using System.Collections.Generic;
using System.Text;

namespace AccessibilityMod.Utilities
{
    /// <summary>
    /// Macht Spieltext fuer den Screenreader lesbarer, wo die reine Anzeige
    /// visuell stilisiert ist.
    ///
    /// Anlass (Jana, 30.07.2026): Der Regisseur (Sal Manella) spricht in der
    /// Vorlage in "Zahlen-Sprech"/Leetspeak — Buchstaben werden durch aehnlich
    /// aussehende Ziffern ersetzt ("Gef4hr" = "Gefahr"). Fuer Sehende ist das eine
    /// lesbare Stilisierung; ein Screenreader liest "Gef-vier-hr" und macht die
    /// Zeilen unverstaendlich. Also drehen wir die Ersetzung fuer die Sprachausgabe
    /// zurueck.
    /// </summary>
    public static class TextNormalizer
    {
        // Bewusst nur die visuell EINDEUTIGEN Leet-Ziffern. 2 und 6 sind mehrdeutig
        // (2=z/to, 6=b/g) und bleiben unangetastet, bis ein echter Fall auftaucht.
        // 1 -> i ist in der Praxis am haeufigsten (Netzjargon), im Zweifel meldet
        // Jana ein falsches Wort und die Zuordnung wird korrigiert.
        private static readonly Dictionary<char, char> LeetMap = new Dictionary<char, char>
        {
            { '0', 'o' },
            { '1', 'i' },
            { '3', 'e' },
            { '4', 'a' },
            { '5', 's' },
            { '7', 't' },
            { '8', 'b' },
            { '9', 'g' },
        };

        /// <summary>
        /// Ersetzt Leet-Ziffern durch ihre Buchstaben — aber NUR innerhalb eines
        /// Wortes (zusammenhaengender Block aus Buchstaben/Ziffern) mit mindestens
        /// zwei Buchstaben. So bleiben echte Zahlen unberuehrt: Uhrzeiten ("14:24"),
        /// Daten ("22. Februar"), Mengen ("Studio 1") stehen isoliert oder als reine
        /// Ziffernbloecke und werden nie angefasst. Getroffen wird nur, was klar ein
        /// stilisiertes Wort ist ("Gef4hr", "l33t").
        /// </summary>
        public static string FixLeetDigits(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Schnell raus, wenn gar keine Ziffer drin ist (der haeufigste Fall).
            bool hasDigit = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] >= '0' && text[i] <= '9')
                {
                    hasDigit = true;
                    break;
                }
            }
            if (!hasDigit)
                return text;

            char[] chars = text.ToCharArray();
            int n = chars.Length;
            int i2 = 0;

            while (i2 < n)
            {
                // Wortgrenze finden: maximaler Block aus Buchstaben/Ziffern.
                if (!IsWordChar(chars[i2]))
                {
                    i2++;
                    continue;
                }

                int start = i2;
                int letters = 0;
                while (i2 < n && IsWordChar(chars[i2]))
                {
                    if (char.IsLetter(chars[i2]))
                        letters++;
                    i2++;
                }
                int end = i2; // exklusiv

                // Nur echte Woerter (>= 2 Buchstaben) entstilisieren. Reine
                // Ziffernbloecke (letters == 0) und Kuerzel wie "3D" (1 Buchstabe)
                // bleiben unangetastet.
                if (letters >= 2)
                {
                    for (int k = start; k < end; k++)
                    {
                        char rep;
                        if (LeetMap.TryGetValue(chars[k], out rep))
                            chars[k] = rep;
                    }
                }
            }

            return new string(chars);
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c);
        }
    }
}
