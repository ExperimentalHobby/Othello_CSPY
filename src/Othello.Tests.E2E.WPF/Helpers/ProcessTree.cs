namespace Technopro.Othello.Tests.E2E.WPF.Helpers;

using System.Management;

/// <summary>
/// プロセスの親子関係を WMI（Win32_Process）で調べるヘルパー。
/// 親が終了した後は子孫を辿れなくなるため、終了前に <see cref="Descendants"/> で記録しておき、
/// 終了後に <see cref="IsRunning"/> で残存を確認する。
/// </summary>
public static class ProcessTree
{
	/// <summary>
	/// プロセスの識別情報。PID は再利用されるため、開始時刻（WMI の CreationDate 文字列）と組で同一性を判定する。
	/// </summary>
	/// <param name="Pid">プロセス ID。</param>
	/// <param name="Name">プロセス名（例: python.exe）。</param>
	/// <param name="CreationDate">WMI の CreationDate（開始時刻）。</param>
	public sealed record ProcessInfo(int Pid, string Name, string CreationDate)
	{
		/// <summary>失敗メッセージ用の表示。</summary>
		public override string ToString() => $"{Name}({Pid})";
	}

	/// <summary>指定プロセスのすべての子孫（子・孫・…）を返す。ルート自身は含まない。</summary>
	/// <param name="rootPid">起点のプロセス ID。</param>
	public static IReadOnlyList<ProcessInfo> Descendants(int rootPid)
	{
		var all = Snapshot();
		var result = new List<ProcessInfo>();
		var queue = new Queue<int>();
		queue.Enqueue(rootPid);
		var visited = new HashSet<int> { rootPid };

		while (queue.Count > 0)
		{
			var parent = queue.Dequeue();
			foreach (var (info, parentPid) in all.Where(p => p.ParentPid == parent))
			{
				// 親 PID が再利用されていると循環し得るため、訪問済みは辿らない
				if (!visited.Add(info.Pid))
					continue;
				result.Add(info);
				queue.Enqueue(info.Pid);
			}
		}
		return result;
	}

	/// <summary>記録したプロセスがまだ実行中か。PID が同じでも開始時刻が異なれば別プロセス（PID 再利用）として false。</summary>
	/// <param name="process"><see cref="Descendants"/> で記録したプロセス。</param>
	public static bool IsRunning(ProcessInfo process)
		=> Snapshot().Any(p => p.Info.Pid == process.Pid && p.Info.CreationDate == process.CreationDate);

	/// <summary>現在の全プロセスを、親 PID 付きで取得する。</summary>
	private static List<(ProcessInfo Info, int ParentPid)> Snapshot()
	{
		using var searcher = new ManagementObjectSearcher("SELECT ProcessId, ParentProcessId, Name, CreationDate FROM Win32_Process");
		using var results = searcher.Get();
		var list = new List<(ProcessInfo, int)>();
		foreach (ManagementBaseObject item in results)
		{
			using (item)
			{
				list.Add((
					new ProcessInfo(
						Convert.ToInt32(item["ProcessId"]),
						item["Name"]?.ToString() ?? "",
						item["CreationDate"]?.ToString() ?? ""),
					Convert.ToInt32(item["ParentProcessId"])));
			}
		}
		return list;
	}
}
