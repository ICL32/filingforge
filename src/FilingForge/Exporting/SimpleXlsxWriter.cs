using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace FilingForge;

public record XlsxCell(string? Text = null, double? Number = null, int StyleId = 0)
{
    public static XlsxCell TextCell(string? value, int styleId = 0) => new(value ?? "", null, styleId);
    public static XlsxCell NumberCell(double? value, int styleId = 0) => new(null, value, styleId);
}

public record XlsxSheet(string Name, IReadOnlyList<IReadOnlyList<XlsxCell>> Rows, bool FreezeTopRow = true, bool AutoFilter = true);

public static class SimpleXlsxWriter
{
    public const int NormalStyle = 0;
    public const int BaseHeaderStyle = 1;
    public const int IncomeHeaderStyle = 2;
    public const int CashFlowHeaderStyle = 3;
    public const int BalanceSheetHeaderStyle = 4;
    public const int SharesHeaderStyle = 5;
    public const int DarkHeaderStyle = 6;

    public static byte[] CreateWorkbook(IReadOnlyList<XlsxSheet> sheets)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypes(sheets.Count));
            WriteEntry(archive, "_rels/.rels", RootRelationships());
            WriteEntry(archive, "xl/workbook.xml", Workbook(sheets));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships(sheets.Count));
            WriteEntry(archive, "xl/styles.xml", Styles());

            for (var i = 0; i < sheets.Count; i++)
                WriteEntry(archive, $"xl/worksheets/sheet{i + 1}.xml", Worksheet(sheets[i]));
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static string Worksheet(XlsxSheet sheet)
    {
        var rowCount = Math.Max(sheet.Rows.Count, 1);
        var colCount = Math.Max(sheet.Rows.Select(r => r.Count).DefaultIfEmpty(1).Max(), 1);
        var lastCell = $"{ColumnName(colCount)}{rowCount}";
        var sb = new StringBuilder();

        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
        sb.Append($"""<dimension ref="A1:{lastCell}"/>""");
        if (sheet.FreezeTopRow)
        {
            sb.Append("""<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>""");
        }
        sb.Append("""<sheetFormatPr defaultRowHeight="15"/>""");
        sb.Append("<cols>");
        for (var col = 1; col <= colCount; col++)
        {
            var width = Math.Clamp(MaxColumnTextWidth(sheet.Rows, col - 1) + 2, 10, 36);
            sb.Append(CultureInfo.InvariantCulture, $"""<col min="{col}" max="{col}" width="{width}" customWidth="1"/>""");
        }
        sb.Append("</cols>");
        sb.Append("<sheetData>");

        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            sb.Append(CultureInfo.InvariantCulture, $"""<row r="{r + 1}">""");
            var row = sheet.Rows[r];
            for (var c = 0; c < row.Count; c++)
                AppendCell(sb, r + 1, c + 1, row[c]);
            sb.Append("</row>");
        }

        sb.Append("</sheetData>");
        if (sheet.AutoFilter && sheet.Rows.Count > 0)
            sb.Append($"""<autoFilter ref="A1:{lastCell}"/>""");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    private static int MaxColumnTextWidth(IReadOnlyList<IReadOnlyList<XlsxCell>> rows, int column)
    {
        var max = 0;
        foreach (var row in rows.Take(200))
        {
            if (column >= row.Count) continue;
            var cell = row[column];
            var length = cell.Text?.Length ?? (cell.Number.HasValue ? 14 : 0);
            if (length > max) max = length;
        }
        return max;
    }

    private static void AppendCell(StringBuilder sb, int row, int col, XlsxCell cell)
    {
        var reference = $"{ColumnName(col)}{row}";
        if (cell.Number.HasValue)
        {
            sb.Append(CultureInfo.InvariantCulture, $"""<c r="{reference}" s="{cell.StyleId}"><v>{cell.Number.Value.ToString("G17", CultureInfo.InvariantCulture)}</v></c>""");
            return;
        }

        if (string.IsNullOrEmpty(cell.Text))
        {
            sb.Append(CultureInfo.InvariantCulture, $"""<c r="{reference}" s="{cell.StyleId}"/>""");
            return;
        }

        sb.Append(CultureInfo.InvariantCulture, $"""<c r="{reference}" t="inlineStr" s="{cell.StyleId}"><is><t>{XmlEscape(cell.Text)}</t></is></c>""");
    }

    private static string ColumnName(int index)
    {
        var dividend = index;
        var columnName = "";
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }
        return columnName;
    }

    private static string ContentTypes(int sheetCount)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">""");
        sb.Append("""<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>""");
        sb.Append("""<Default Extension="xml" ContentType="application/xml"/>""");
        sb.Append("""<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>""");
        sb.Append("""<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""");
        for (var i = 1; i <= sheetCount; i++)
            sb.Append(CultureInfo.InvariantCulture, $"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
        sb.Append("</Types>");
        return sb.ToString();
    }

    private static string RootRelationships() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";

    private static string Workbook(IReadOnlyList<XlsxSheet> sheets)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets>""");
        for (var i = 0; i < sheets.Count; i++)
            sb.Append(CultureInfo.InvariantCulture, $"""<sheet name="{XmlEscape(sheets[i].Name)}" sheetId="{i + 1}" r:id="rId{i + 1}"/>""");
        sb.Append("</sheets></workbook>");
        return sb.ToString();
    }

    private static string WorkbookRelationships(int sheetCount)
    {
        var sb = new StringBuilder();
        sb.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""");
        sb.Append("""<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
        for (var i = 1; i <= sheetCount; i++)
            sb.Append(CultureInfo.InvariantCulture, $"""<Relationship Id="rId{i}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i}.xml"/>""");
        sb.Append(CultureInfo.InvariantCulture, $"""<Relationship Id="rId{sheetCount + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""");
        sb.Append("</Relationships>");
        return sb.ToString();
    }

    private static string Styles() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="2">
            <font><sz val="11"/><color theme="1"/><name val="Calibri"/><family val="2"/></font>
            <font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/><family val="2"/></font>
          </fonts>
          <fills count="7">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF475569"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF2563EB"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF059669"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFD97706"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF7C3AED"/><bgColor indexed="64"/></patternFill></fill>
          </fills>
          <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
          <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
          <cellXfs count="7">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFill="1" applyFont="1"/>
            <xf numFmtId="0" fontId="1" fillId="3" borderId="0" xfId="0" applyFill="1" applyFont="1"/>
            <xf numFmtId="0" fontId="1" fillId="4" borderId="0" xfId="0" applyFill="1" applyFont="1"/>
            <xf numFmtId="0" fontId="1" fillId="5" borderId="0" xfId="0" applyFill="1" applyFont="1"/>
            <xf numFmtId="0" fontId="1" fillId="6" borderId="0" xfId="0" applyFill="1" applyFont="1"/>
            <xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFill="1" applyFont="1"/>
          </cellXfs>
          <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
        </styleSheet>
        """.TrimStart();

    private static string XmlEscape(string value) =>
        System.Security.SecurityElement.Escape(value) ?? "";
}
