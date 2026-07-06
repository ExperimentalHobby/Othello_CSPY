using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Technopro.Othello.ViewModels;

/// <summary>
/// <see cref="INotifyPropertyChanged"/> を実装する ViewModel 基底クラス。
/// <see cref="SetProperty{T}"/> で値変更時に自動で <see cref="PropertyChanged"/> を発火する。
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}

	protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
	{
		if (Equals(storage, value))
			return false;

		storage = value;
		OnPropertyChanged(propertyName);
		return true;
	}
}
