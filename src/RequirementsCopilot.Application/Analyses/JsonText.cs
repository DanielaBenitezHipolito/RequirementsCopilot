namespace RequirementsCopilot.Application.Analyses;

/// <summary>
/// Utilidades para leer el JSON que devuelve un LLM, que puede venir con prosa alrededor, vallas de
/// código (```), o el mismo objeto DUPLICADO (algunos modelos/gateways lo emiten dos veces).
/// </summary>
public static class JsonText
{
    /// <summary>
    /// Extrae el PRIMER objeto JSON balanceado del texto (por conteo de llaves, ignorando las que
    /// están dentro de cadenas). Así se evita mezclar objetos duplicados o texto sobrante.
    /// </summary>
    /// <param name="text">Texto que puede contener un objeto JSON.</param>
    /// <returns>El primer objeto <c>{...}</c> completo, o <c>null</c> si no hay uno cerrado.</returns>
    public static string? FirstJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        int start = text.IndexOf('{');
        if (start < 0)
        {
            return null;
        }

        int depth = 0;
        bool inString = false;
        bool escaped = false;
        for (int i = start; i < text.Length; i++)
        {
            char current = text[i];
            if (inString)
            {
                (inString, escaped) = AdvanceString(current, escaped);
                continue;
            }

            if (current == '"')
            {
                inString = true;
            }
            else if (current == '{')
            {
                depth++;
            }
            else if (current == '}' && --depth == 0)
            {
                return text[start..(i + 1)];
            }
        }

        return null;
    }

    /// <summary>
    /// Avanza el estado del escáner mientras se está DENTRO de una cadena JSON, según el carácter
    /// actual: consume el escape pendiente, detecta la barra invertida o el cierre de comillas.
    /// </summary>
    /// <param name="current">Carácter actual.</param>
    /// <param name="escaped">Si el carácter anterior abrió un escape (<c>\</c>).</param>
    /// <returns>El nuevo par (¿seguimos en cadena?, ¿escape pendiente?).</returns>
    private static (bool InString, bool Escaped) AdvanceString(char current, bool escaped)
    {
        if (escaped)
        {
            return (true, false);
        }

        if (current == '\\')
        {
            return (true, true);
        }

        return current == '"' ? (false, false) : (true, false);
    }
}
