using System.Text.RegularExpressions;

namespace Kapea.Domain.Tests.Conventions;

/// <summary>
/// Detecta texto visible para el usuario escrito directamente en un .razor.
/// Todo ese texto debe venir de los ficheros de recursos, así que aquí se
/// elimina del marcado cuanto no es texto visible (código, comentarios,
/// etiquetas, expresiones Razor) y se comprueba que no queda nada.
/// </summary>
internal static partial class UiLiteralScanner
{
    /// <summary>Atributos cuyo valor lee una persona y por tanto debe estar localizado.</summary>
    private static readonly string[] UserFacingAttributes = ["title", "placeholder", "alt", "aria-label"];

    internal static IReadOnlyList<string> FindLiterals(string markup)
    {
        var findings = new List<string>();

        var withoutDirectives = Directive().Replace(markup, " ");
        var withoutCode = RemoveCodeBlocks(withoutDirectives);
        var withoutComments = RazorComment().Replace(withoutCode, " ");
        var withoutScripts = ScriptOrStyle().Replace(withoutComments, " ");

        foreach (Match attribute in UserFacingAttribute().Matches(withoutScripts))
        {
            var name = attribute.Groups["name"].Value.ToLowerInvariant();
            var value = attribute.Groups["value"].Value.Trim();

            if (UserFacingAttributes.Contains(name) && value.Length > 0 && !value.StartsWith('@'))
            {
                findings.Add($"{name}=\"{value}\"");
            }
        }

        var textOnly = Tag().Replace(withoutScripts, " ");
        var withoutControlFlow = ControlFlow().Replace(textOnly, " ");
        var withoutExpressions = RazorExpression().Replace(withoutControlFlow, " ");
        var withoutBraces = Braces().Replace(withoutExpressions, " ");

        findings.AddRange(withoutBraces
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => Letter().IsMatch(line)));

        return findings;
    }

    /// <summary>
    /// Recorta los bloques @code emparejando llaves: una expresión regular no
    /// distingue la llave que cierra el bloque de las que hay dentro del método.
    /// </summary>
    private static string RemoveCodeBlocks(string markup)
    {
        var result = markup;

        while (true)
        {
            var start = result.IndexOf("@code", StringComparison.Ordinal);

            if (start < 0)
            {
                return result;
            }

            var open = result.IndexOf('{', start);

            if (open < 0)
            {
                return result[..start];
            }

            var depth = 0;
            var index = open;

            for (; index < result.Length; index++)
            {
                if (result[index] == '{')
                {
                    depth++;
                }
                else if (result[index] == '}' && --depth == 0)
                {
                    break;
                }
            }

            result = result[..start] + (index < result.Length ? result[(index + 1)..] : string.Empty);
        }
    }

    /// <summary>
    /// Directivas de línea completa. Se quitan antes que nada porque su cola
    /// (una ruta, un tipo, un espacio de nombres) no es texto visible y el
    /// resto del análisis la confundiría con un literal sin localizar.
    /// </summary>
    [GeneratedRegex(@"^\s*@(page|layout|using|inject|inherits|implements|attribute|typeparam|namespace|rendermode|preservewhitespace|addTagHelper|removeTagHelper|tagHelperPrefix).*$", RegexOptions.Multiline)]
    private static partial Regex Directive();

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline)]
    private static partial Regex RazorComment();

    [GeneratedRegex(@"<(script|style)\b.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScriptOrStyle();

    [GeneratedRegex(@"(?<name>[A-Za-z-]+)\s*=\s*""(?<value>[^""]*)""")]
    private static partial Regex UserFacingAttribute();

    /// <summary>
    /// Etiqueta completa, saltando por encima de los valores entrecomillados. Sin eso,
    /// un lambda en un atributo —OnClick="@(() =&gt; Borrar())"— cierra la etiqueta en su
    /// flecha y el resto del atributo se leería como texto visible.
    /// </summary>
    [GeneratedRegex("""<(?:[^>"']|"[^"]*"|'[^']*')*>""", RegexOptions.Singleline)]
    private static partial Regex Tag();

    /// <summary>
    /// Control de flujo de Razor con su condición. La arroba es opcional porque en un
    /// encadenamiento solo la lleva el primero: el else que sigue a una llave va suelto.
    /// </summary>
    [GeneratedRegex(@"@?\b(?:else\s+if|if|foreach|for|while|switch|do|try|catch|finally|else)\b\s*(?:\((?:[^()]|\([^()]*\))*\))?")]
    private static partial Regex ControlFlow();

    /// <summary>Llaves sueltas de los bloques de control, que no son texto de nadie.</summary>
    [GeneratedRegex(@"[{}]")]
    private static partial Regex Braces();

    /// <summary>
    /// Expresiones Razor: @Body, @Ui["Clave"], @(expresión) y cadenas de llamadas como
    /// @fecha.ToLocalTime().ToString("d"), donde el formato entre comillas no es texto
    /// para nadie aunque lo parezca.
    /// </summary>
    [GeneratedRegex("""@\w+(?:\((?:[^()]|\([^()]*\))*\)|\[[^\]]*\])?(?:\.\w+(?:\((?:[^()]|\([^()]*\))*\)|\[[^\]]*\])?)*|@\((?:[^()]|\([^()]*\))*\)"""  )]
    private static partial Regex RazorExpression();

    [GeneratedRegex(@"\p{L}")]
    private static partial Regex Letter();
}
