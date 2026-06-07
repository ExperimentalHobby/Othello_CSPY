"""
ai.py - Python AI エントリポイント

C# の PythonSubprocessAI から stdin 経由で JSON リクエストを受け取り、
アルファベータ探索で最善手を計算して stdout に JSON で返す。

IPC プロトコル（改行区切り JSON）:
    リクエスト: {"board": [[int,...]], "player": int, "depth": int, "time_ms": int|null}
        board   : 8×8 の int 配列（0=Empty, 1=Black, 2=White）
        player  : AI が担当するプレイヤーの色（1=Black, 2=White）
        depth   : アルファベータ探索の最大深さ（DifficultyLevel に応じて C# 側が設定）
        time_ms : 反復深化の制限時間（ms）。Hard=8000、Easy/Normal=null（固定深さ探索）

    レスポンス（成功）: {"row": int, "col": int}
    レスポンス（エラー）: {"error": string}

プロセスのライフサイクル:
    C# が起動 → stdin が閉じられるまでリクエストを処理し続ける → 自然終了
"""

import sys
import json
from alpha_beta import AlphaBetaAI


def main():
    """
    stdin を 1 行ずつ読み込んで AI を実行し、結果を stdout に書き出すメインループ。
    stdin が閉じられると for ループが終了し、プロセスが正常終了する。
    """
    # stdin を utf-8-sig モードに再設定して BOM（﻿）を自動除去する。
    # C# 側の Encoding.UTF8 が BOM を先行送信した場合でも正しく処理できる。
    try:
        sys.stdin.reconfigure(encoding='utf-8-sig')
    except AttributeError:
        pass  # Python 3.7 未満では reconfigure が存在しない（3.8+ では常に利用可能）

    # AlphaBetaAI はステートレスなので 1 インスタンスで全リクエストを処理できる
    ai = AlphaBetaAI()

    for line in sys.stdin:
        line = line.strip()
        if not line:
            # 空行は無視する（パイプ経由での改行コードの差異に対応）
            continue

        try:
            # JSON リクエストを解析してパラメータを取り出す
            req    = json.loads(line)
            # 必須フィールドの存在チェック（KeyError より明確なエラーを返す）
            if 'board' not in req or 'player' not in req or 'depth' not in req:
                missing = [k for k in ('board', 'player', 'depth') if k not in req]
                raise ValueError(f"Missing required fields: {missing}")
            board   = req['board']           # 8×8 の int 配列
            player  = req['player']          # 1=Black, 2=White
            depth   = req['depth']           # 探索深さ
            time_ms = req.get('time_ms')     # Hard 時のみ設定される（None=固定深さ探索）

            # time_ms が指定されている場合は反復深化探索、そうでなければ固定深さ探索
            if time_ms is not None:
                move = ai.get_best_move_timed(board, player, depth, time_ms)
            else:
                move = ai.get_best_move(board, player, depth)

            if move is None:
                # 有効手なし → C# 側でパス処理が必要なことを通知する
                response = json.dumps({'error': 'no valid moves'})
            else:
                # 最善手の座標を JSON で返す
                response = json.dumps({'row': move[0], 'col': move[1]})

        except Exception as e:
            # 予期せぬ例外はエラーとして C# 側に通知する（プロセスは継続する）
            response = json.dumps({'error': str(e)})

        # flush=True で即座に出力する（C# の ReadLine がブロッキング待機しているため必須）
        print(response, flush=True)


if __name__ == '__main__':
    main()
