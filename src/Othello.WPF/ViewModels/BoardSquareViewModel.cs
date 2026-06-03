using System.Windows.Media;
using Technopro.Othello.Core.Models;

namespace Technopro.Othello.WPF.ViewModels;

/// <summary>
/// ボード上の 1 マス（セル）を表す ViewModel。
/// 8×8 = 64 個のインスタンスが GameViewModel.BoardSquares に格納され、
/// MainWindow.xaml の ItemsControl にバインドされる。
/// 石の色・有効手ハイライトの状態を保持し、UI への変更通知を行う。
/// </summary>
public class BoardSquareViewModel : ViewModelBase
{
    /// <summary>このマスの盤面上の座標（コンストラクタで設定後は不変）</summary>
    public Position Position { get; }

    /// <summary>このマスに置かれている石の色（Empty / Black / White）</summary>
    private PlayerColor _piece = PlayerColor.Empty;

    /// <summary>このマスが有効な着手位置かどうか（黄色ハイライト表示に使用）</summary>
    private bool _isValidMove;

    /// <summary>このマスの石を表示するブラシ色</summary>
    private SolidColorBrush _pieceColor = new(Colors.Transparent);

    /// <summary>石が存在するかどうか（Ellipse の Visibility 制御に使用）</summary>
    private bool _hasPiece;

    /// <summary>このマスの石の色。変更時に WPF バインディングへ通知する。</summary>
    public PlayerColor Piece { get => _piece; set => SetProperty(ref _piece, value); }

    /// <summary>
    /// このマスが有効な着手位置かどうか。
    /// true の場合 XAML 側で黄色の Ellipse を表示する。
    /// </summary>
    public bool IsValidMove { get => _isValidMove; set => SetProperty(ref _isValidMove, value); }

    /// <summary>石の描画に使用するブラシ（黒/白/透明）</summary>
    public SolidColorBrush PieceColor { get => _pieceColor; set => SetProperty(ref _pieceColor, value); }

    /// <summary>
    /// 石が置かれているかどうか。
    /// false の場合 XAML 側で石の Ellipse を非表示にする。
    /// </summary>
    public bool HasPiece { get => _hasPiece; set => SetProperty(ref _hasPiece, value); }

    /// <summary>
    /// 指定した座標で BoardSquareViewModel を生成する。
    /// </summary>
    /// <param name="position">このマスの盤面座標（0-7, 0-7）</param>
    public BoardSquareViewModel(Position position)
    {
        Position = position;
    }

    /// <summary>
    /// 指定した PlayerColor に応じてマスの表示状態（石の色・存在フラグ）を更新する。
    /// GameViewModel.RefreshBoardDisplay から全 64 マスに対して呼ばれる。
    /// </summary>
    /// <param name="piece">このマスに置かれている石の色</param>
    public void SetPiece(PlayerColor piece)
    {
        Piece = piece;
        // Empty でなければ石が存在する
        HasPiece = piece != PlayerColor.Empty;

        // 石の色に応じてブラシを切り替える（Empty は透明で石を非表示にする）
        PieceColor = piece switch
        {
            PlayerColor.Black => new SolidColorBrush(Colors.Black),
            PlayerColor.White => new SolidColorBrush(Colors.White),
            _                 => new SolidColorBrush(Colors.Transparent) // Empty
        };
    }
}
