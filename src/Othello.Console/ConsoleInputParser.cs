namespace Technopro.Othello.Console;

using Technopro.Othello.Core.Models;

/// <summary>
/// コンソール入力文字列を Position に変換するパーサ。
/// "d4" 形式（列文字 a-h + 行番号 1-8）と "3 4" 形式（行列インデックス 0-7）をサポートする。
/// </summary>
internal static class ConsoleInputParser
{
    /// <summary>
    /// 文字列入力を Position に変換する。
    /// "d4" 形式（列文字 a-h + 行番号 1-8）または "3 4" 形式（行列インデックス 0-7）をサポートする。
    /// </summary>
    /// <param name="input">変換対象の文字列（小文字変換済みを想定）</param>
    /// <returns>有効な座標であれば Position、不正入力であれば null</returns>
    public static Position? ParseInput(string input)
    {
        try
        {
            // "d4" 形式: 先頭が文字、2 文字目が数字の場合
            if (input.Length == 2 && char.IsLetter(input[0]) && char.IsDigit(input[1]))
            {
                int col = input[0] - 'a'; // 'a'=0, 'b'=1, ... 'h'=7
                int row = input[1] - '1'; // '1'=0, '2'=1, ... '8'=7
                if (Position.IsValid(row, col))
                    return new Position(row, col);
            }

            // "3 4" 形式: スペース区切りで 2 つの整数
            var parts = input.Split(' ');
            if (parts.Length == 2 && int.TryParse(parts[0], out int r) && int.TryParse(parts[1], out int c))
            {
                if (Position.IsValid(r, c))
                    return new Position(r, c);
            }
        }
        catch
        {
            // 例外はすべて null 返却で吸収する
        }

        return null;
    }

    /// <summary>
    /// 列インデックス（0-7）を対応する列文字（'a'-'h'）に変換する。
    /// </summary>
    /// <param name="col">列インデックス（0=a, 7=h）</param>
    /// <returns>対応する列文字</returns>
    public static char ColChar(int col) => (char)('a' + col);
}
