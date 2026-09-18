using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fitab.Api.Json;

/// <summary>
/// Booleani del backend Fitab. Li esprime in almeno tre modi diversi a seconda
/// dell'endpoint: "S"/"N" (SonoIo, SonoQui, SOLODANESI), 1/0 (Visibile, Attivo)
/// e -1/0 (convenzione interna Instant Developer). Li accettiamo tutti.
/// </summary>
public sealed class FitabBooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Null => false,
            JsonTokenType.Number => reader.TryGetInt64(out var n) && n != 0,
            JsonTokenType.String => Parse(reader.GetString()),
            _ => false
        };

    private static bool Parse(string? s) => s?.Trim().ToUpperInvariant() switch
    {
        "S" or "SI" or "Y" or "TRUE" or "1" or "-1" => true,
        _ => false
    };

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value ? "S" : "N");
}

/// <summary>
/// Date del backend: normalmente "2026-09-10", ma i campi opzionali arrivano
/// come null, stringa vuota o spazio singolo. Nessuna di queste deve far fallire
/// la deserializzazione dell'intera risposta.
/// </summary>
public sealed class FitabDateOnlyConverter : JsonConverter<DateOnly?>
{
    private static readonly string[] Formati =
    [
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm:ss",
        "dd/MM/yyyy",
        "dd/MM/yyyy HH:mm:ss"
    ];

    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.Null) return null;
        var s = reader.GetString()?.Trim();
        if (string.IsNullOrEmpty(s)) return null;

        if (DateTime.TryParseExact(s, Formati, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var esatta))
            return DateOnly.FromDateTime(esatta);

        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var libera)
            ? DateOnly.FromDateTime(libera)
            : null;
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}

/// <summary>
/// Interi tolleranti: alcuni campi numerici arrivano come stringa (o come stringa
/// vuota quando il valore non e' valorizzato).
/// </summary>
public sealed class FitabIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.TryGetInt32(out var n) ? n : 0,
            JsonTokenType.String => int.TryParse(reader.GetString()?.Trim(),
                NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : 0,
            _ => 0
        };

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value);
}

/// <summary>
/// Stringhe tolleranti: qualche campo identificativo (es. IdRegione) arriva
/// a volte come numero e a volte come stringa con zero iniziale.
/// </summary>
public sealed class FitabStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString()?.Trim() ?? "",
            JsonTokenType.Number => reader.TryGetInt64(out var n)
                ? n.ToString(CultureInfo.InvariantCulture)
                : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
            JsonTokenType.True => "S",
            JsonTokenType.False => "N",
            _ => ""
        };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
