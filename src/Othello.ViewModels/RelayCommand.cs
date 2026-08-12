using System.Windows.Input;

namespace Technopro.Othello.ViewModels;

/// <summary>パラメーターなしのコマンドを実装する <see cref="ICommand"/> 汎用実装。</summary>
public class RelayCommand : ICommand
{
	private readonly Action _execute;
	private readonly Func<bool>? _canExecute;

	private event EventHandler? _canExecuteChanged;

	public event EventHandler? CanExecuteChanged
	{
		add { _canExecuteChanged += value; }
		remove { _canExecuteChanged -= value; }
	}

	/// <summary>コマンドを初期化する。</summary>
	/// <param name="execute">コマンド実行時に呼ばれるアクション。</param>
	/// <param name="canExecute">実行可否を返すデリゲート。省略時は常に実行可能。</param>
	public RelayCommand(Action execute, Func<bool>? canExecute = null)
	{
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
		_canExecute = canExecute;
	}

	public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

	public void Execute(object? parameter) => _execute();

	/// <summary>
	/// CanExecute の再評価を要求する。呼び出し元（ViewModel）が状態変化のタイミングで明示的に呼ぶ。
	/// </summary>
	public void RaiseCanExecuteChanged() => _canExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>型付きパラメーターを持つコマンドを実装する <see cref="ICommand"/> 汎用実装。</summary>
public class RelayCommand<T> : ICommand
{
	private readonly Action<T?> _execute;
	private readonly Func<T?, bool>? _canExecute;

	private event EventHandler? _canExecuteChanged;

	public event EventHandler? CanExecuteChanged
	{
		add { _canExecuteChanged += value; }
		remove { _canExecuteChanged -= value; }
	}

	/// <summary>コマンドを初期化する。</summary>
	/// <param name="execute">コマンド実行時に呼ばれるアクション（型付きパラメーターを受け取る）。</param>
	/// <param name="canExecute">実行可否を返すデリゲート。省略時は常に実行可能。</param>
	public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
	{
		_execute = execute ?? throw new ArgumentNullException(nameof(execute));
		_canExecute = canExecute;
	}

	public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;

	public void Execute(object? parameter) => _execute((T?)parameter);

	/// <summary>
	/// CanExecute の再評価を要求する。呼び出し元（ViewModel）が状態変化のタイミングで明示的に呼ぶ。
	/// </summary>
	public void RaiseCanExecuteChanged() => _canExecuteChanged?.Invoke(this, EventArgs.Empty);
}
