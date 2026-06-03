using System.Windows.Input;

namespace Technopro.Othello.WPF.ViewModels;

/// <summary>
/// パラメータなしのコマンドを実装する汎用クラス。
/// ViewModel のメソッドを ICommand としてバインドするために使用する。
/// CanExecute の評価は WPF の CommandManager に委ねる。
/// </summary>
public class RelayCommand : ICommand
{
    /// <summary>コマンド実行時に呼び出すアクション</summary>
    private readonly Action _execute;

    /// <summary>コマンドが実行可能かどうかを返す関数（null の場合は常に実行可能）</summary>
    private readonly Func<bool>? _canExecute;

    /// <summary>
    /// CanExecute の再評価を要求するイベント。
    /// CommandManager.RequerySuggested に接続することで WPF が自動的に再評価する。
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add    { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    /// <summary>
    /// 実行アクションと有効条件を指定して RelayCommand を生成する。
    /// </summary>
    /// <param name="execute">コマンド実行時のアクション（必須）</param>
    /// <param name="canExecute">実行可能条件の関数（省略時は常に true）</param>
    /// <exception cref="ArgumentNullException">execute が null の場合</exception>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// コマンドが実行可能かどうかを返す。
    /// </summary>
    /// <param name="parameter">使用しない（パラメータなしコマンドのため）</param>
    /// <returns>_canExecute が null または true を返す場合 true</returns>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <summary>
    /// コマンドを実行する。
    /// </summary>
    /// <param name="parameter">使用しない</param>
    public void Execute(object? parameter) => _execute();
}

/// <summary>
/// 型付きパラメータを受け取るコマンドを実装する汎用クラス。
/// SquareClickedCommand など、クリックされた座標などを引数に取るコマンドに使用する。
/// </summary>
/// <typeparam name="T">コマンドパラメータの型</typeparam>
public class RelayCommand<T> : ICommand
{
    /// <summary>コマンド実行時に呼び出すアクション（型付きパラメータあり）</summary>
    private readonly Action<T?> _execute;

    /// <summary>コマンドが実行可能かどうかを返す関数（null の場合は常に実行可能）</summary>
    private readonly Func<T?, bool>? _canExecute;

    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add    { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    /// <summary>
    /// 型付き実行アクションと有効条件を指定して RelayCommand を生成する。
    /// </summary>
    /// <param name="execute">コマンド実行時のアクション</param>
    /// <param name="canExecute">実行可能条件の関数（省略時は常に true）</param>
    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// コマンドが実行可能かどうかを返す。
    /// </summary>
    /// <param name="parameter">型 T にキャストして _canExecute へ渡すパラメータ</param>
    /// <returns>_canExecute が null または true を返す場合 true</returns>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

    /// <summary>
    /// コマンドを実行する。
    /// </summary>
    /// <param name="parameter">型 T にキャストして _execute へ渡すパラメータ</param>
    public void Execute(object? parameter) => _execute((T?)parameter);
}
