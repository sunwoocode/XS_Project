using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class CardData
{
    public const int DefaultTagPoint = 2;
    public const int DefaultCost = 1;
    public const string DefaultCardName = "카드";
    public const string DefaultEffectText = "카드의 효과를 사용합니다.";

    public static readonly Color DefaultRaceColor = new(179f / 255f, 175f / 255f, 175f / 255f, 1f);

    public string Index { get; }
    public string CardName { get; }
    public int TagPoint { get; }
    public int Cost { get; }
    public string ImagePath { get; }
    public string EffectText { get; }
    public Color RaceColor { get; }

    public CardData(
        string index = "",
        string cardName = DefaultCardName,
        int tagPoint = DefaultTagPoint,
        int cost = DefaultCost,
        string imagePath = "",
        string effectText = DefaultEffectText,
        Color? raceColor = null)
    {
        Index = index?.Trim() ?? string.Empty;
        CardName = string.IsNullOrWhiteSpace(cardName) ? DefaultCardName : cardName.Trim();
        TagPoint = Mathf.Max(0, tagPoint);
        Cost = Mathf.Max(0, cost);
        ImagePath = imagePath?.Trim() ?? string.Empty;
        EffectText = string.IsNullOrWhiteSpace(effectText) ? DefaultEffectText : effectText;
        RaceColor = raceColor ?? DefaultRaceColor;
    }
}

public static class CardCsvParser
{
    private static readonly string[] ExpectedHeaders =
    {
        "Index", "cardName", "tagPoint", "cost", "imagePath", "effectText", "raceColor"
    };

    public static List<CardData> Parse(string csv, Action<string> warning = null)
    {
        List<CsvRecord> records = ReadRecords(csv ?? string.Empty, warning);
        List<CardData> cards = new();
        if (records.Count == 0)
        {
            warning?.Invoke("카드 CSV가 비어 있습니다. 카드가 생성되지 않습니다.");
            return cards;
        }

        Dictionary<string, int> columns = BuildColumnMap(records[0], warning);
        if (!columns.ContainsKey("Index"))
        {
            return cards;
        }

        HashSet<string> usedIndices = new(StringComparer.Ordinal);
        for (int i = 1; i < records.Count; i++)
        {
            CsvRecord record = records[i];
            if (IsBlank(record.Fields))
            {
                continue;
            }

            string index = GetValue(record, columns, "Index", string.Empty, warning, false).Trim();
            if (string.IsNullOrEmpty(index))
            {
                warning?.Invoke($"CSV {record.Line}행 {GetDisplayColumn(columns, "Index")}열: 'Index'가 비어 있어 해당 카드를 제외합니다.");
                continue;
            }

            if (!usedIndices.Add(index))
            {
                warning?.Invoke($"CSV {record.Line}행 {GetDisplayColumn(columns, "Index")}열: 중복 Index '{index}'는 첫 번째 카드만 사용합니다.");
                continue;
            }

            string cardName = GetValue(record, columns, "cardName", CardData.DefaultCardName, warning, true).Trim();
            int tagPoint = ParseNonNegativeInt(record, columns, "tagPoint", CardData.DefaultTagPoint, warning);
            int cost = ParseNonNegativeInt(record, columns, "cost", CardData.DefaultCost, warning);
            string imagePath = GetValue(record, columns, "imagePath", string.Empty, warning, false).Trim();
            string effectText = GetValue(record, columns, "effectText", CardData.DefaultEffectText, warning, true);
            Color raceColor = ParseColor(record, columns, warning);
            cards.Add(new CardData(index, cardName, tagPoint, cost, imagePath, effectText, raceColor));
        }

        return cards;
    }

    private static Dictionary<string, int> BuildColumnMap(CsvRecord header, Action<string> warning)
    {
        Dictionary<string, int> columns = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < header.Fields.Count; i++)
        {
            string name = header.Fields[i].Trim();
            if (i == 0)
            {
                name = name.TrimStart('\uFEFF');
            }

            if (!string.IsNullOrEmpty(name) && !columns.TryAdd(name, i))
            {
                warning?.Invoke($"CSV {header.Line}행 {i + 1}열: 중복 헤더 '{name}'은 첫 번째 열을 사용합니다.");
            }
        }

        foreach (string expected in ExpectedHeaders)
        {
            if (!columns.ContainsKey(expected))
            {
                string behavior = expected == "Index"
                    ? "모든 카드 행을 제외합니다."
                    : "해당 필드는 기본값을 사용합니다.";
                warning?.Invoke($"CSV {header.Line}행: 필수 헤더 '{expected}'가 없어 {behavior}");
            }
        }

        return columns;
    }

    private static int ParseNonNegativeInt(
        CsvRecord record,
        IReadOnlyDictionary<string, int> columns,
        string name,
        int fallback,
        Action<string> warning)
    {
        string value = GetValue(record, columns, name, fallback.ToString(CultureInfo.InvariantCulture), warning, true);
        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) && parsed >= 0)
        {
            return parsed;
        }

        warning?.Invoke($"CSV {record.Line}행 {GetDisplayColumn(columns, name)}열: '{name}' 값 '{value}'이(가) 0 이상의 정수가 아니므로 기본값 {fallback}을 사용합니다.");
        return fallback;
    }

    private static Color ParseColor(
        CsvRecord record,
        IReadOnlyDictionary<string, int> columns,
        Action<string> warning)
    {
        string fallback = "179|175|175";
        string value = GetValue(record, columns, "raceColor", fallback, warning, true);
        string[] channels = value.Split('|');
        if (channels.Length == 3 &&
            byte.TryParse(channels[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte red) &&
            byte.TryParse(channels[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte green) &&
            byte.TryParse(channels[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out byte blue))
        {
            return new Color32(red, green, blue, 255);
        }

        warning?.Invoke($"CSV {record.Line}행 {GetDisplayColumn(columns, "raceColor")}열: 'raceColor' 값 '{value}'이(가) R|G|B 형식이 아니므로 기본값 {fallback}을 사용합니다.");
        return CardData.DefaultRaceColor;
    }

    private static string GetValue(
        CsvRecord record,
        IReadOnlyDictionary<string, int> columns,
        string name,
        string fallback,
        Action<string> warning,
        bool warnWhenBlank)
    {
        if (!columns.TryGetValue(name, out int index))
        {
            return fallback;
        }

        if (index >= record.Fields.Count)
        {
            warning?.Invoke($"CSV {record.Line}행 {index + 1}열: '{name}' 값이 누락되어 기본값을 사용합니다.");
            return fallback;
        }

        string value = record.Fields[index];
        if (warnWhenBlank && string.IsNullOrWhiteSpace(value))
        {
            warning?.Invoke($"CSV {record.Line}행 {index + 1}열: '{name}' 값이 비어 있어 기본값을 사용합니다.");
            return fallback;
        }

        return value;
    }

    private static int GetDisplayColumn(IReadOnlyDictionary<string, int> columns, string name)
    {
        return columns.TryGetValue(name, out int index) ? index + 1 : 0;
    }

    private static bool IsBlank(IReadOnlyList<string> fields)
    {
        for (int i = 0; i < fields.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(fields[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static List<CsvRecord> ReadRecords(string csv, Action<string> warning)
    {
        List<CsvRecord> records = new();
        List<string> fields = new();
        StringBuilder field = new();
        bool quoted = false;
        int line = 1;
        int recordLine = 1;

        for (int i = 0; i < csv.Length; i++)
        {
            char character = csv[i];
            if (quoted)
            {
                if (character == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    if (character == '\r')
                    {
                        if (i + 1 < csv.Length && csv[i + 1] == '\n')
                        {
                            i++;
                        }

                        field.Append('\n');
                        line++;
                    }
                    else
                    {
                        field.Append(character);
                        if (character == '\n')
                        {
                            line++;
                        }
                    }
                }

                continue;
            }

            if (character == '"' && field.Length == 0)
            {
                quoted = true;
            }
            else if (character == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (character == '\r' || character == '\n')
            {
                fields.Add(field.ToString());
                field.Clear();
                records.Add(new CsvRecord(recordLine, new List<string>(fields)));
                fields.Clear();
                if (character == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                {
                    i++;
                }

                line++;
                recordLine = line;
            }
            else
            {
                field.Append(character);
            }
        }

        if (quoted)
        {
            warning?.Invoke($"CSV {recordLine}행: 닫히지 않은 따옴표를 파일 끝에서 닫힌 것으로 처리합니다.");
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add(new CsvRecord(recordLine, new List<string>(fields)));
        }

        return records;
    }

    private sealed class CsvRecord
    {
        public int Line { get; }
        public List<string> Fields { get; }

        public CsvRecord(int line, List<string> fields)
        {
            Line = line;
            Fields = fields;
        }
    }
}
