UI のスタイリングを更新します。

## WPF リソースファイル

色・スタイルは以下のファイルで一元管理されています:
- `src/Othello.GUI/WPF/Resources/AppColors.xaml` — SolidColorBrush リソース
- `src/Othello.GUI/WPF/Resources/AppStyles.xaml` — スタイル（Button/ComboBox/Border）

### カラーパレット

| キー                      | カラーコード | 用途                         |
|---------------------------|-------------|------------------------------|
| `WindowBackgroundBrush`   | `#2d5016`   | ウィンドウ背景               |
| `MenuDarkGreenBrush`      | `#1a3010`   | メニューバー・サイドバー・ステータスバー |
| `BoardGreenBrush`         | `#4a7c2f`   | ボード・ボタン背景           |
| `ButtonHoverGreenBrush`   | `#5a8c3f`   | ボタンホバー                 |
| `BorderDarkGreenBrush`    | `#2a5a1f`   | ComboBox 枠線                |
| `ComboBoxItemHoverBrush`  | `#e8f5e9`   | ComboBox 項目ホバー          |
| `ValidMoveHighlightBrush` | `Yellow`    | 合法手ハイライト             |
| `SeparatorBrush`          | `#666666`   | セパレータ                   |
| `AiBadgeBackgroundBrush`  | `#1a1a2e`   | AI バッジ背景                |
| `AiBadgeForegroundBrush`  | `#7ec8e3`   | AI バッジ文字色              |

### スタイルキー

| キー                   | 対象          | 用途                     |
|------------------------|---------------|--------------------------|
| `ActionButtonStyle`    | `Button`      | メニューバーボタン       |
| `MenuComboBoxStyle`    | `ComboBox`    | メニューバー ComboBox    |
| `MenuComboBoxItemStyle`| `ComboBoxItem`| ComboBox ドロップダウン項目 |
| `SidebarPanelStyle`    | `Border`      | サイドパネル枠           |

## WinUI3

WinUI3 では `src/Othello.GUI/WinUI3/MainWindow.xaml` の `Grid.Resources` 内に色リソースが定義されています。
WPF のリソースキーと対応する色コードを使用して変更してください。

## 注意事項

- ダークグリーンテーマの統一感を維持すること
- ComboBox ドロップダウンは可読性のため白背景・黒文字を維持すること
- 変更後は `dotnet run --project src/Othello.GUI/WPF/Othello.WPF.csproj` で目視確認すること

どのスタイルを変更しますか？
