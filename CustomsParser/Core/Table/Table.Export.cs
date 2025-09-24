using System.Text.Json;

namespace PdfTableMvp.Core
{
    public partial class Table
    {
        public void ExportToJson(string path)
        {
            var list = ToObjects();
            var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        public List<Dictionary<string, string>> ToObjects()
        {
            var list = new List<Dictionary<string, string>>(Rows.Count);
            for (int r = 0; r < Rows.Count; r++)
            {
                var row = Rows[r];
                var obj = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < ColumnNames.Count; c++)
                {
                    string key = ColumnNames[c];
                    string val = c < row.Length ? (row[c] ?? "") : "";
                    obj[key] = val;
                }
                list.Add(obj);
            }
            return list;
        }
    }
}
