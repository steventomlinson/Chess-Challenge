using ChessChallenge.API;
using System.Linq;
using System;
using System.Collections.Generic;

public class MyBot : IChessBot
{
    int[] pieceValues = { 0, 100, 300, 300, 500, 900, 0 };
    Board currentBoard;
    struct Entry
    {
        public bool occupied = false;
        public int value = 0;
        public int depth = 0;
        public Entry() { }
    };
    Entry[] table;

    int evaluate()
    {
        int sum = 0;
        foreach (PieceType type in Enumerable.Range(1, 6))
            sum += pieceValues[(int)type] * (currentBoard.GetPieceList(type, true).Count - currentBoard.GetPieceList(type, false).Count);

        return sum * (currentBoard.IsWhiteToMove ? 1 : -1);
    }
    int nodes = 0;

    void order(Move[] moves)
    {
        var move_value = new Dictionary<Move, int>();
        foreach (Move move in moves)
        {
            int score = 0;
            if (move.IsCapture)
                score += 10 * pieceValues[(int)move.CapturePieceType] - pieceValues[(int)move.MovePieceType];
            if (move.IsPromotion)
                score += pieceValues[(int)move.PromotionPieceType];

            if (currentBoard.SquareIsAttackedByOpponent(move.TargetSquare))
                score -= pieceValues[(int)move.MovePieceType];

            move_value[move] = score;
        }

        Array.Sort(moves, (m1, m2) => { return move_value[m2] - move_value[m1]; });
    }

    int quiesce(int alpha, int beta)
    {
        var entry = table[currentBoard.ZobristKey % (ulong)table.Length];
        if (entry.occupied)
        {
            return Math.Max(entry.value, alpha);
        }

        int val = evaluate();
        if (val >= beta)
            return beta;

        if (alpha < val)
            alpha = val;

        var moves = currentBoard.GetLegalMoves(true);
        order(moves);
        foreach (Move move in moves)
        {
            nodes++;
            currentBoard.MakeMove(move);
            val = -quiesce(-beta, -alpha);
            currentBoard.UndoMove(move);
            if (val > alpha)
                alpha = val;
            if (alpha >= beta) break;
        }
        return alpha;
    }

    int minimax(int alpha, int beta, int depth, Action<Move> action)
    {
        var entry = table[currentBoard.ZobristKey % (ulong)table.Length];
        if (entry.occupied && depth < entry.depth)
        {
            return Math.Max(entry.value, alpha);
        }
        if (depth == 0) return quiesce(alpha, beta);
        if (currentBoard.IsDraw()) return 0;
        // This allows us to always be making progress to checkmate when checkmate is available
        // before we would move back and forth between positions until forced to due to a.
        if (currentBoard.IsInCheckmate()) return -999999 - depth;

        var moves = currentBoard.GetLegalMoves();
        order(moves);

        foreach (Move move in moves)
        {
            nodes++;
            currentBoard.MakeMove(move);
            int val = -minimax(-beta, -alpha, depth - 1, (move) => { });
            currentBoard.UndoMove(move);

            if (val > alpha)
            {
                alpha = val;
                action(move);
            }
            if (alpha >= beta) break;
        }
        entry.occupied = true;
        entry.value = alpha;
        entry.depth = depth;
        return alpha;
    }

    public Move Think(Board board, Timer timer)
    {
        table = new Entry[1024 * 1024 * 100];
        currentBoard = board;
        Move retMove = Move.NullMove;
        nodes = 0;
        var val = minimax(-1073741824, 1073741824, 5, (move) => { retMove = move; });
        Console.WriteLine($"Good Val {val} Searched {nodes}");
        return retMove;
    }
}