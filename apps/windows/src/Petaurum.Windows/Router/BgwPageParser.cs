using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Petaurum.Windows.Router;

/// <summary>
/// Turns an AT&amp;T BGW status page into <see cref="RouterPageData"/>: each h2 starts a section, and each table
/// becomes fields, records or a grid. See specs/features/router.md, "Reading".
/// </summary>
public static class BgwPageParser
{
    /// <summary>Whether the router answered with its access code form instead of the page.</summary>
    public static bool NeedsAccessCode(IDocument document) =>
        LoginForm(document) is not null;

    /// <summary>
    /// Whether this is the access code page. Without a session cookie it says cookies must be enabled and has no form;
    /// fetching again (with the cookie it just set) brings the form.
    /// </summary>
    public static bool IsAccessCodePage(IDocument document) =>
        document.QuerySelectorAll("h1").Any(h => h.TextContent.Contains("Access Code Required", StringComparison.OrdinalIgnoreCase));

    public static IHtmlFormElement? LoginForm(IDocument document) =>
        document.QuerySelector("form[action*='login.ha']") as IHtmlFormElement;

    public static RouterPageData Parse(IDocument document)
    {
        var content = document.GetElementById("content-sub") ?? document.GetElementById("content") ?? document.Body;
        var sections = new List<RouterSection>();
        var current = new RouterSection(null, []);
        if (content is null) return new RouterPageData(sections);

        foreach (var element in content.QuerySelectorAll("h2, table, div.status-label"))
        {
            switch (element)
            {
                case { LocalName: "h2" }:
                    var heading = TextOf(element);
                    if (heading.Equals("Help", StringComparison.OrdinalIgnoreCase)) goto done;
                    Add(sections, current);
                    current = new RouterSection(heading, []);
                    break;
                case IHtmlTableElement table when table.ParentElement?.Closest("table") is null:
                    current.Blocks.AddRange(ParseTable(table));
                    break;
                case { LocalName: "div" } when element.NextElementSibling is { } value && value.ClassList.Contains("status-value"):
                    AddField(current, new RouterField(TextOf(element), TextOf(value)));
                    break;
            }
        }
        done:
        Add(sections, current);
        return new RouterPageData(sections);
    }

    private static void Add(List<RouterSection> sections, RouterSection section)
    {
        if (section.Blocks.Count > 0) sections.Add(section);
    }

    private static void AddField(RouterSection section, RouterField field)
    {
        if (section.Blocks is [.., { Kind: RouterBlockKind.Fields } last]) last.Fields!.Add(field);
        else section.Blocks.Add(new RouterBlock(RouterBlockKind.Fields, Fields: [field]));
    }

    private sealed record Cell(bool IsHeader, bool IsColumnHeader, string Text);

    private static IEnumerable<RouterBlock> ParseTable(IHtmlTableElement table)
    {
        var rows = new List<List<Cell>?>(); // null marks a rule between records
        foreach (var row in table.Rows)
        {
            if (row.QuerySelector("hr") is not null) { rows.Add(null); continue; }
            var cells = row.Cells
                .Select(c => new Cell(c.LocalName == "th", c.GetAttribute("scope") == "col", TextOf(c)))
                .ToList();
            // Drop trailing cells that only held buttons.
            while (cells.Count > 1 && cells[^1].Text.Length == 0) cells.RemoveAt(cells.Count - 1);
            if (cells.Count > 0 && cells.Any(c => c.Text.Length > 0)) rows.Add(cells);
        }
        if (rows.Count == 0) yield break;

        var dataRows = rows.OfType<List<Cell>>().ToList();
        if (IsGrid(dataRows))
        {
            yield return ParseGrid(dataRows);
            yield break;
        }

        // Label/value rows; rules split them into records.
        var groups = new List<List<RouterField>> { new() };
        foreach (var row in rows)
        {
            if (row is null) { if (groups[^1].Count > 0) groups.Add([]); continue; }
            // A lone header cell is a label whose value the router left empty; a lone data cell is a note.
            var labelled = row.Count > 1 || row[0].IsHeader;
            var label = labelled ? row[0].Text : "";
            var value = string.Join("\n", (labelled ? row.Skip(1) : row).Select(c => c.Text).Where(t => t.Length > 0));
            groups[^1].Add(new RouterField(label, value));
        }
        groups.RemoveAll(g => g.Count == 0);
        yield return groups.Count > 1
            ? new RouterBlock(RouterBlockKind.Records, Records: groups.Select(g => new RouterRecord(g)).ToList())
            : new RouterBlock(RouterBlockKind.Fields, Fields: groups[0]);
    }

    /// <summary>A grid has a heading row: column-scoped header cells, or a first row of two or more header cells.</summary>
    private static bool IsGrid(List<List<Cell>> rows) =>
        rows.Any(r => r.Any(c => c.IsColumnHeader)) || rows[0] is { Count: >= 2 } first && first.All(c => c.IsHeader);

    private static RouterBlock ParseGrid(List<List<Cell>> rows)
    {
        var headerIndex = rows.FindIndex(r => r.Any(c => c.IsColumnHeader));
        if (headerIndex < 0) headerIndex = 0;
        var header = rows[headerIndex];
        // Some pages leave out a </tr>, so the first data row runs on after the headings: split it off.
        // The corner cell above the row labels is often an empty td.
        var headingCount = header.TakeWhile(c => c.IsHeader || c.Text.Length == 0).Count();
        var columns = header.Take(headingCount).Select(c => c.Text).ToList();
        var body = new List<List<string>>();
        if (header.Count > headingCount) body.Add(header.Skip(headingCount).Select(c => c.Text).ToList());
        body.AddRange(rows.Skip(headerIndex + 1).Select(r => r.Select(c => c.Text).ToList()));
        // Rows before the heading row (rare) are kept as rows too.
        body.InsertRange(0, rows.Take(headerIndex).Select(r => r.Select(c => c.Text).ToList()));
        return new RouterBlock(RouterBlockKind.Table, Columns: columns, Rows: body);
    }

    /// <summary>
    /// An element's text as the router shows it: line breaks kept, image alt text included,
    /// form controls, scripts and styles left out.
    /// </summary>
    public static string TextOf(INode node)
    {
        var builder = new StringBuilder();
        Append(node, builder);
        var lines = builder.ToString().Replace(' ', ' ').Split('\n')
            .Select(line => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(line => line.Length > 0);
        return string.Join("\n", lines);
    }

    private static void Append(INode node, StringBuilder builder)
    {
        switch (node)
        {
            case IText text:
                builder.Append(node.Parent is IHtmlPreElement ? text.Data : text.Data.Replace('\n', ' '));
                return;
            case IElement { LocalName: "br" }:
                builder.Append('\n');
                return;
            case IElement { LocalName: "img" } image:
                if (image.GetAttribute("alt") is { Length: > 0 } alt) builder.Append(' ').Append(alt).Append(' ');
                return;
            case IElement { LocalName: "input" or "select" or "button" or "textarea" or "script" or "style" or "noscript" }:
                return;
            case IElement { LocalName: "p" or "div" or "pre" or "tr" or "li" }:
                foreach (var child in node.ChildNodes) Append(child, builder);
                builder.Append('\n');
                return;
            default:
                foreach (var child in node.ChildNodes) Append(child, builder);
                return;
        }
    }
}
