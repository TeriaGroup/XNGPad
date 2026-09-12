using System.Text;

namespace XNGPad.Core.Services;

public static class SearxngSettingsPatcher
{
    public static bool HasJsonFormat(string yaml)
    {
        var inFormats = false;
        foreach (var raw in SplitLines(yaml))
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0)
            {
                if (inFormats)
                    inFormats = false;
                continue;
            }

            if (trimmed.StartsWith('#'))
                continue;

            var stripped = StripInlineComment(trimmed);
            if (stripped.StartsWith("formats:", StringComparison.Ordinal))
            {
                if (ContainsJsonToken(stripped))
                    return true;
                inFormats = true;
                continue;
            }

            if (inFormats)
            {
                if (stripped.StartsWith('-'))
                {
                    var item = stripped[1..].Trim().Trim('"', '\'');
                    if (item.Equals("json", StringComparison.OrdinalIgnoreCase))
                        return true;
                    continue;
                }

                inFormats = false;
            }
        }

        return false;
    }

    public static string EnableJsonFormat(string yaml)
    {
        if (HasJsonFormat(yaml))
            return yaml;

        var lines = SplitLines(yaml);
        var nl = yaml.Contains("\r\n") ? "\r\n" : "\n";
        var formatsIndex = -1;
        var lastItemIndex = -1;
        var searchIndex = -1;

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('#'))
                continue;

            var stripped = StripInlineComment(trimmed);
            if (stripped.StartsWith("search:") && searchIndex < 0)
                searchIndex = i;

            if (stripped.StartsWith("formats:", StringComparison.Ordinal))
            {
                formatsIndex = i;
                lastItemIndex = i;
                if (stripped.Length > "formats:".Length)
                {
                    var rest = stripped["formats:".Length..].Trim();
                    if (rest.StartsWith('['))
                    {
                        lines[i] = InsertIntoInlineList(lines[i]);
                        return Join(lines, nl, yaml);
                    }
                }

                for (var j = i + 1; j < lines.Count; j++)
                {
                    var t = lines[j].Trim();
                    if (t.StartsWith('#'))
                        continue;
                    if (t.StartsWith('-'))
                    {
                        lastItemIndex = j;
                        continue;
                    }

                    break;
                }

                break;
            }
        }

        if (formatsIndex >= 0)
        {
            var indent = ItemIndent(lines, formatsIndex, lastItemIndex);
            var insertAt = lastItemIndex + 1;
            lines.Insert(insertAt, indent + "- json");
            return Join(lines, nl, yaml);
        }

        if (searchIndex >= 0)
        {
            var indent = LeadingSpaces(lines[searchIndex]);
            var child = indent + "  ";
            lines.Insert(searchIndex + 1, child + "formats:");
            lines.Insert(searchIndex + 2, child + "  - html");
            lines.Insert(searchIndex + 3, child + "  - json");
            return Join(lines, nl, yaml);
        }

        var suffix = yaml.Length > 0 && !yaml.EndsWith('\n') ? nl : "";
        return yaml + suffix + "search:" + nl + "  formats:" + nl + "    - html" + nl + "    - json" + nl;
    }

    private static string InsertIntoInlineList(string line)
    {
        var hash = line.IndexOf('#');
        var code = hash >= 0 ? line[..hash] : line;
        var comment = hash >= 0 ? line[hash..] : "";
        var open = code.IndexOf('[');
        var close = code.LastIndexOf(']');
        if (open < 0 || close < open)
            return line.TrimEnd() + (line.Contains(':') ? " [html, json]" : "") + (comment.Length > 0 ? " " + comment : "");

        var inner = code[(open + 1)..close].Trim();
        var prefix = code[..(open + 1)];
        var suffix = code[close..];
        if (string.IsNullOrWhiteSpace(inner))
            return prefix + "html, json" + suffix + comment;
        return prefix + inner + ", json" + suffix + comment;
    }

    private static string ItemIndent(List<string> lines, int formatsIndex, int lastItemIndex)
    {
        if (lastItemIndex != formatsIndex)
        {
            var lead = LeadingSpaces(lines[lastItemIndex]);
            if (lead.Length > 0)
                return lead;
        }

        return LeadingSpaces(lines[formatsIndex]) + "  ";
    }

    private static string LeadingSpaces(string line)
    {
        var i = 0;
        while (i < line.Length && line[i] == ' ')
            i++;
        return line[..i];
    }

    private static bool ContainsJsonToken(string formatsLine)
    {
        var rest = formatsLine["formats:".Length..];
        foreach (var part in rest.Split(new[] { '[', ']', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Trim().Equals("json", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string StripInlineComment(string line)
    {
        var inSingle = false;
        var inDouble = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '\'' && !inDouble)
                inSingle = !inSingle;
            else if (c == '"' && !inSingle)
                inDouble = !inDouble;
            else if (c == '#' && !inSingle && !inDouble)
                return line[..i].TrimEnd();
        }

        return line;
    }

    private static List<string> SplitLines(string text)
    {
        var list = new List<string>();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
            list.Add(line);
        return list;
    }

    private static string Join(List<string> lines, string nl, string original)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < lines.Count; i++)
        {
            if (i > 0)
                sb.Append(nl);
            sb.Append(lines[i]);
        }

        if (original.EndsWith('\n'))
            sb.Append(nl);
        return sb.ToString();
    }
}
