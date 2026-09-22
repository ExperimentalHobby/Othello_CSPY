namespace Technopro.Othello.Tests.ViewModels;

using Technopro.Othello.ViewModels;

/// <summary>
/// RelayCommand / RelayCommand&lt;T&gt; の単体テスト（Issue #198）。
/// CanExecuteChanged イベントの add/remove・RaiseCanExecuteChanged の発火・
/// パラメーター付き CanExecute を検証する。
/// </summary>
public class RelayCommandTests
{
	// ===== RelayCommand =====

	/// <summary>
	/// CanExecuteChanged にハンドラを add した状態で RaiseCanExecuteChanged() を呼ぶと発火することを確認する。
	/// パス条件: ハンドラが 1 回呼ばれること。
	/// </summary>
	[Fact]
	public void RaiseCanExecuteChanged_HandlerAdded_InvokesHandler()
	{
		var command = new RelayCommand(() => { });
		int callCount = 0;
		command.CanExecuteChanged += (_, _) => callCount++;

		command.RaiseCanExecuteChanged();

		Assert.Equal(1, callCount);
	}

	/// <summary>
	/// add したハンドラを remove した後は RaiseCanExecuteChanged() を呼んでも発火しないことを確認する。
	/// パス条件: ハンドラが 1 度も呼ばれないこと。
	/// </summary>
	[Fact]
	public void RaiseCanExecuteChanged_HandlerRemoved_DoesNotInvokeHandler()
	{
		var command = new RelayCommand(() => { });
		int callCount = 0;
		void Handler(object? s, EventArgs e) => callCount++;

		command.CanExecuteChanged += Handler;
		command.CanExecuteChanged -= Handler;
		command.RaiseCanExecuteChanged();

		Assert.Equal(0, callCount);
	}

	/// <summary>
	/// canExecute を省略した RelayCommand は常に CanExecute が true を返すことを確認する。
	/// </summary>
	[Fact]
	public void CanExecute_NoDelegateProvided_AlwaysReturnsTrue()
	{
		var command = new RelayCommand(() => { });

		Assert.True(command.CanExecute(null));
	}

	// ===== RelayCommand<T> =====

	/// <summary>
	/// RelayCommand&lt;T&gt;.CanExecute が canExecute デリゲートの戻り値をそのまま反映することを確認する。
	/// パス条件: パラメーターに応じて true/false が返ること。
	/// </summary>
	[Fact]
	public void CanExecuteT_DelegateProvided_ReflectsDelegateResult()
	{
		var command = new RelayCommand<int>(_ => { }, p => p > 0);

		Assert.True(command.CanExecute(5));
		Assert.False(command.CanExecute(-1));
	}

	/// <summary>
	/// canExecute を省略した RelayCommand&lt;T&gt; は常に CanExecute が true を返すことを確認する。
	/// </summary>
	[Fact]
	public void CanExecuteT_NoDelegateProvided_AlwaysReturnsTrue()
	{
		var command = new RelayCommand<int>(_ => { });

		Assert.True(command.CanExecute(0));
	}

	/// <summary>
	/// RelayCommand&lt;T&gt;.CanExecuteChanged にハンドラを add した状態で
	/// RaiseCanExecuteChanged() を呼ぶと発火することを確認する。
	/// パス条件: ハンドラが 1 回呼ばれること。
	/// </summary>
	[Fact]
	public void RaiseCanExecuteChangedT_HandlerAdded_InvokesHandler()
	{
		var command = new RelayCommand<int>(_ => { });
		int callCount = 0;
		command.CanExecuteChanged += (_, _) => callCount++;

		command.RaiseCanExecuteChanged();

		Assert.Equal(1, callCount);
	}

	/// <summary>
	/// add したハンドラを remove した後は RaiseCanExecuteChanged() を呼んでも発火しないことを確認する。
	/// パス条件: ハンドラが 1 度も呼ばれないこと。
	/// </summary>
	[Fact]
	public void RaiseCanExecuteChangedT_HandlerRemoved_DoesNotInvokeHandler()
	{
		var command = new RelayCommand<int>(_ => { });
		int callCount = 0;
		void Handler(object? s, EventArgs e) => callCount++;

		command.CanExecuteChanged += Handler;
		command.CanExecuteChanged -= Handler;
		command.RaiseCanExecuteChanged();

		Assert.Equal(0, callCount);
	}
}
