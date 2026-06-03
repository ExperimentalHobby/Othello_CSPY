using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Technopro.Othello.WPF.ViewModels;

/// <summary>
/// WPF の MVVM パターンにおける ViewModel 基底クラス。
/// INotifyPropertyChanged を実装し、プロパティ変更通知のヘルパーメソッドを提供する。
/// すべての ViewModel はこのクラスを継承して SetProperty を使用する。
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <summary>プロパティ変更を WPF バインディングエンジンに通知するイベント</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 指定したプロパティ名で PropertyChanged イベントを発火する。
    /// </summary>
    /// <param name="propertyName">
    /// 変更されたプロパティの名前。
    /// CallerMemberName により呼び出し元のプロパティ名が自動設定される。
    /// </param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// フィールドの値が変わった場合のみ更新し、PropertyChanged を発火するヘルパー。
    /// 値が変わらない場合はイベントを発火せず、不要な UI 再描画を抑制する。
    /// </summary>
    /// <typeparam name="T">プロパティの型</typeparam>
    /// <param name="storage">バッキングフィールドへの参照</param>
    /// <param name="value">新しい値</param>
    /// <param name="propertyName">プロパティ名（CallerMemberName で自動設定）</param>
    /// <returns>値が実際に変更された場合 true、変更なしの場合 false</returns>
    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        // 値が変わらない場合はイベント発火をスキップして余分な再描画を防ぐ
        if (Equals(storage, value))
            return false;

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
