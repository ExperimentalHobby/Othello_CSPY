using Technopro.Othello.Core.Models;

namespace Technopro.Othello.ViewModels;

/// <summary>
/// ボード上の 1 マスを表す ViewModel。
/// Piece / HasPiece / IsValidMove を保持し、UI への変更通知を行う。
/// 色の解決（SolidColorBrush 等）は各 UI 層のコンバーターが担う。
/// </summary>
public class BoardSquareViewModel : ViewModelBase
{
    public Position Position { get; }

    private PlayerColor _piece = PlayerColor.Empty;
    private bool _isValidMove;
    private bool _hasPiece;
    private bool _isBeingFlipped;
    private bool _isHint;

    public PlayerColor Piece { get => _piece; set => SetProperty(ref _piece, value); }

    public bool IsValidMove { get => _isValidMove; set => SetProperty(ref _isValidMove, value); }

    public bool HasPiece { get => _hasPiece; set => SetProperty(ref _hasPiece, value); }

    /// <summary>着手後の反転アニメーション中であることを示すフラグ。UI 層がアニメーションを駆動するために使用する。</summary>
    public bool IsBeingFlipped { get => _isBeingFlipped; set => SetProperty(ref _isBeingFlipped, value); }

    /// <summary>AI 推奨手（ヒント）であることを示すフラグ。人間のターン時のみ true になる。</summary>
    public bool IsHint { get => _isHint; set => SetProperty(ref _isHint, value); }

    /// <summary>指定した盤面座標でマスを初期化する。</summary>
    /// <param name="position">このマスが対応する盤面上の座標（0〜7, 0〜7）。</param>
    public BoardSquareViewModel(Position position)
    {
        Position = position;
    }

    /// <summary>
    /// 石を設定し、<see cref="HasPiece"/> を連動更新する。
    /// <see cref="HasPiece"/> は石の有無を示す UI バインディング用フラグ。
    /// </summary>
    /// <param name="piece">設定する石の色。<see cref="PlayerColor.Empty"/> で石を除去する。</param>
    public void SetPiece(PlayerColor piece)
    {
        Piece = piece;
        HasPiece = piece != PlayerColor.Empty;
    }
}
