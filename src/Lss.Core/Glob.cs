namespace Lss;

/// <summary>
/// Maschere dei nomi: "*" qualsiasi sequenza di caratteri, "?" un carattere.
/// Scritta a mano, senza Regex. Usata per le regole dei salvataggi e, nella
/// fase N, per gli spazi con maschera.
/// </summary>
internal static class Glob
{
    /// <summary>true se il nome contiene "*" o "?".</summary>
    public static bool HasWildcard(string segment) => segment.Contains('*') || segment.Contains('?');

    /// <summary>
    /// true se il nome corrisponde alla maschera. ignoreCase: il confronto
    /// non distingue maiuscole e minuscole.
    /// </summary>
    public static bool IsMatch(string pattern, string name, bool ignoreCase)
    {
        int p = 0;
        int n = 0;
        int starPattern = -1;
        int starName = -1;

        while (n < name.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || SameChar(pattern[p], name[n], ignoreCase)))
            {
                p++;
                n++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                // Il "*" per ora non prende niente: si riprova più avanti se serve.
                starPattern = p++;
                starName = n;
            }
            else if (starPattern >= 0)
            {
                p = starPattern + 1;
                n = ++starName;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
            p++;

        return p == pattern.Length;
    }

    private static bool SameChar(char a, char b, bool ignoreCase) =>
        ignoreCase ? char.ToUpperInvariant(a) == char.ToUpperInvariant(b) : a == b;
}
