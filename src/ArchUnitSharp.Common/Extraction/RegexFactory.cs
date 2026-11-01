namespace ArchUnitSharp.Common.Extraction;

using System.Text.RegularExpressions;

/// <summary>
/// The single place in the library where a glob is compiled to a <see cref="Regex"/>. Nothing
/// downstream ever sees a glob; everything downstream consumes a <see cref="Pattern"/> or a
/// <see cref="Filter"/>, both of which are built on top of the regex this type produces.
/// </summary>
/// <remarks>
/// <para>
/// The wildcard vocabulary is: <c>*</c> matches any run of characters within one path segment,
/// <c>**</c> matches any number of path segments (including none), <c>?</c> matches exactly one
/// character within a segment, <c>[...]</c> is a character class (<c>[!...]</c> negates it), and
/// <c>(**)</c> is the capturing form of <c>**</c> — it matches the same text and captures it, which
/// is how the library's slices are named. Matching is case-sensitive. The whole glob is anchored, so
/// a glob matches a whole candidate, not a substring of one. A trailing <c>/**</c> includes the
/// base path itself as well as its descendants, so <c>src/Models/**</c> also matches <c>src/Models</c>.
/// </para>
/// <para>
/// Separators are normalised before the glob is compiled: a backslash is treated as the same
/// separator as a forward slash, so a rule behaves identically on every operating system. The
/// candidate string being matched is expected to use forward slashes as its separators.
/// </para>
/// <para>
/// This type is stateless and safe for concurrent use. The <see cref="Regex"/> instances it returns
/// are safe to share across threads.
/// </para>
/// </remarks>
public static class RegexFactory
{
    /// <summary>
    /// Compiles the given glob to an anchored, case-sensitive <see cref="Regex"/>.
    /// </summary>
    /// <param name="glob">The glob to compile. Must not be <see langword="null"/>.</param>
    /// <returns>A <see cref="Regex"/> that matches exactly the candidates the glob describes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="glob"/> is <see langword="null"/>.</exception>
    public static Regex CompileGlob(string glob)
    {
        ArgumentNullException.ThrowIfNull(glob);
        if (glob.IndexOf('\\') >= 0)
        {
            glob = glob.Replace('\\', '/');
        }

        var pattern = new System.Text.StringBuilder(glob.Length * 2 + 2);
        pattern.Append('^');

        int i = 0;
        while (i < glob.Length)
        {
            char c = glob[i];
            switch (c)
            {
                case '*':
                    if (i + 1 < glob.Length && glob[i + 1] == '*')
                    {
                        if (i + 2 < glob.Length && glob[i + 2] == '/')
                        {
                            pattern.Append("(?:[^/]+/)*");
                            i += 3;
                        }
                        else
                        {
                            pattern.Append(".*");
                            i += 2;
                        }
                    }
                    else
                    {
                        pattern.Append("[^/]*");
                        i += 1;
                    }

                    break;

                case '?':
                    pattern.Append("[^/]");
                    i += 1;
                    break;

                case '[':
                    int close = glob.IndexOf(']', i + 1);
                    if (close < 0)
                    {
                        pattern.Append(Regex.Escape("["));
                        i += 1;
                        break;
                    }

                    string characterClass = glob.Substring(i, close - i + 1);
                    if (characterClass.Length > 1 && characterClass[1] == '!')
                    {
                        characterClass = "[^" + characterClass.Substring(2);
                    }

                    pattern.Append(characterClass);
                    i = close + 1;
                    break;

                case '(':
                    if (i + 3 < glob.Length && glob[i + 1] == '*' && glob[i + 2] == '*' && glob[i + 3] == ')')
                    {
                        if (i + 4 < glob.Length && glob[i + 4] == '/')
                        {
                            pattern.Append("((?:[^/]+/)*)");
                            i += 5;
                        }
                        else
                        {
                            pattern.Append("(.*)");
                            i += 4;
                        }
                    }
                    else
                    {
                        pattern.Append(Regex.Escape(c.ToString()));
                        i += 1;
                    }

                    break;

                case '/':
                    if (i + 3 == glob.Length && glob[i + 1] == '*' && glob[i + 2] == '*')
                    {
                        pattern.Append("(?:/.*)?");
                        i += 3;
                    }
                    else
                    {
                        pattern.Append('/');
                        i += 1;
                    }

                    break;

                default:
                    pattern.Append(Regex.Escape(c.ToString()));
                    i += 1;
                    break;
            }
        }

        pattern.Append('$');
        return new Regex(pattern.ToString(), RegexOptions.CultureInvariant);
    }
}
