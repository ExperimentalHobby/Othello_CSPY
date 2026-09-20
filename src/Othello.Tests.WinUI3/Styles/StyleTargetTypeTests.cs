namespace Technopro.Othello.Tests.WinUI3.Styles;

using System.Xml.Linq;

/// <summary>
/// WinUI3 の XAML に定義された Style の TargetType と、その Style を実際に適用している
/// 要素の型が一致していることを静的に検証する（Issue #157）。
///
/// WPF では ToggleButton に Button 用 Style（TargetType="Button"）を誤って適用すると
/// 実行時に XamlParseException でクラッシュする事象が過去に発生している。WinUI3 でも
/// Style.TargetType の不一致は同様に実行時エラーの原因になりうるが、ビルドは通ってしまい
/// 気づきにくい。SolidColorBrush 等の実 XAML ランタイム生成を要するテストは本プロジェクトの
/// テストホストでは実行できない（ConverterTests.cs 冒頭のコメント参照）ため、
/// XAML を XML として静的解析することで実行時コストなしに同種の不整合を検知する。
/// </summary>
public class StyleTargetTypeTests
{
	private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
	private static readonly XNamespace XamlDirective = "http://schemas.microsoft.com/winfx/2006/xaml";

	private static string XamlDirectory =>
		Path.Combine(AppContext.BaseDirectory, "WinUI3Xaml");

	private static IEnumerable<string> AllXamlFiles() =>
		Directory.EnumerateFiles(XamlDirectory, "*.xaml");

	/// <summary>
	/// 各 XAML ファイル内で `Style="{StaticResource X}"` を使用している要素すべてについて、
	/// 参照先の `<Style x:Key="X" TargetType="Y">` の TargetType が、実際にその Style を
	/// 適用している要素の型と一致することを確認する。
	/// パス条件: 全ファイル・全要素で TargetType 不一致が 0 件であること。
	/// </summary>
	[Fact]
	public void StaticResourceStyles_TargetTypeMatchesAppliedElement()
	{
		var mismatches = new List<string>();

		foreach (var path in AllXamlFiles())
		{
			var doc = XDocument.Load(path);
			var root = doc.Root;
			if (root is null) continue;

			// x:Key を持つ Style 定義を "キー名 → TargetType" のマップにする
			var styleTargetTypes = root.Descendants(Xaml + "Style")
				.Where(s => s.Attribute(XamlDirective + "Key") is not null)
				.ToDictionary(
					s => s.Attribute(XamlDirective + "Key")!.Value,
					s => s.Attribute("TargetType")?.Value);

			// Style="{StaticResource キー名}" を持つ全要素を検査する
			foreach (var element in root.Descendants())
			{
				var styleAttr = element.Attribute("Style")?.Value;
				if (styleAttr is null) continue;

				var key = ExtractStaticResourceKey(styleAttr);
				if (key is null || !styleTargetTypes.TryGetValue(key, out var targetType) || targetType is null)
					continue;

				var elementTypeName = element.Name.LocalName;
				if (elementTypeName != targetType)
				{
					mismatches.Add(
						$"{Path.GetFileName(path)}: <{elementTypeName}> に TargetType=\"{targetType}\" の " +
						$"Style \"{key}\" が適用されています（一致しません）");
				}
			}
		}

		Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
	}

	/// <summary>"{StaticResource キー名}" からキー名部分だけを取り出す。該当しなければ null。</summary>
	private static string? ExtractStaticResourceKey(string styleAttributeValue)
	{
		const string prefix = "{StaticResource ";
		if (!styleAttributeValue.StartsWith(prefix, StringComparison.Ordinal) || !styleAttributeValue.EndsWith("}", StringComparison.Ordinal))
			return null;

		return styleAttributeValue[prefix.Length..^1].Trim();
	}
}
