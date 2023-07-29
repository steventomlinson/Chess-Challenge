using ChessChallenge.API;
using System.Linq;
using System;
using System.Collections.Generic;

public class MyBot : IChessBot
{
    struct Entry
    {
        public int value = 0;
        public Move move = Move.NullMove;
        public int depth = 0;
        public int type = 0;
        public Entry() { }
    }
    Entry[] table = new Entry[1024 * 1024 * 100];

    readonly int[] pieceValues = { 0, 100, 300, 300, 500, 900, 20000 };
    Board currentBoard;

    int rootDepth;
    int currentDepth;
    Timer currentTimer;

    ulong TableHash => currentBoard.ZobristKey % (ulong)table.LongLength;

    Entry TableEntry => table[TableHash];

    int Evaluate()
    {
        int sum = 0;
        foreach (PieceType type in Enumerable.Range(1, 6))
            sum += pieceValues[(int)type] * (currentBoard.GetPieceList(type, true).Count - currentBoard.GetPieceList(type, false).Count);

        return sum * (currentBoard.IsWhiteToMove ? 1 : -1);
    }
    int nodes = 0;
    int qNodes = 0;
    int lookups = 0;

    void Order(Move[] moves)
    {
        var move_value = new Dictionary<Move, int>();
        foreach (Move move in moves)
        {
            int score = 0;
            if (TableEntry.move == move)
            {
                score = 32001;
            }
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

    int GetStoredValue(int alpha, int beta)
    {
        if (TableEntry.move != Move.NullMove && currentDepth <= TableEntry.depth)
        {
            if (TableEntry.type == 1)
            {
                lookups++;
                return TableEntry.value;
            }
            if (TableEntry.type == 0 && TableEntry.value <= alpha)
            {
                lookups++;
                return TableEntry.value;
            }
            if (TableEntry.type == 2 && TableEntry.value >= beta)
            {
                lookups++;
                return TableEntry.value;
            }
        }
        return 999999;
    }

    int StoreValue(Move move, int type, int value)
    {
        table[TableHash].move = move;
        table[TableHash].depth = currentDepth;
        table[TableHash].type = type;
        return table[TableHash].value = value;
    }

    int Quiesce(int alpha, int beta)
    {
        int bestValue = GetStoredValue(alpha, beta);
        if (bestValue != 999999)
            return bestValue;

        bestValue = Evaluate();
        if (bestValue >= beta) return bestValue;
        if (alpha < bestValue)
            alpha = bestValue;


        var moves = currentBoard.GetLegalMoves(true);
        Order(moves);
        foreach (Move move in moves)
        {
            nodes++;
            qNodes++;
            currentBoard.MakeMove(move);
            int val = -Quiesce(-beta, -alpha);
            currentBoard.UndoMove(move);
            bestValue = Math.Max(val, bestValue);
            if (bestValue >= beta) break;
            if (bestValue > alpha)
                alpha = bestValue;
        }
        return bestValue;
    }

    int AlphaBetaSearch(int alpha, int beta)
    {
        if (currentBoard.IsDraw()) return 0;
        int bestValue = GetStoredValue(alpha, beta);
        if (bestValue != 999999)
            return bestValue;

        if (currentDepth == 0) return Quiesce(alpha, beta);
        // This allows us to always be making progress to checkmate when checkmate is available
        // before we would move back and forth between positions until forced to due to a.
        if (currentBoard.IsInCheckmate()) return -32001 + (rootDepth - currentDepth);

        var moves = currentBoard.GetLegalMoves();
        Order(moves);
        bestValue = -32001;
        Move bestMove = Move.NullMove;
        int type = 0;

        foreach (Move move in moves)
        {
            nodes++;
            currentBoard.MakeMove(move);
            currentDepth--;
            int val = -AlphaBetaSearch(-beta, -alpha);
            currentDepth++;
            currentBoard.UndoMove(move);
            if (currentTimer.MillisecondsElapsedThisTurn > 200)
                return bestValue;
            if (val > bestValue)
            {
                bestValue = val;
                bestMove = move;
                if (val > alpha)
                {
                    if (val >= beta)
                        return StoreValue(move, 2, bestValue);
                    alpha = val;
                    type = 1;
                }
            }
        }
        return StoreValue(bestMove, type, bestValue);
    }

    int IterDeepen()
    {
        int bestValue = -32001;
        int val = bestValue;
        rootDepth = 0;
        while (true)
        {
            currentDepth = rootDepth;
            var window = 10;
            while (true)
            {
                var alpha = Math.Max(val - window, -32001);
                var beta = Math.Min(val + window, 32001);
                val = AlphaBetaSearch(alpha, beta);
                if (currentTimer.MillisecondsElapsedThisTurn > 200)
                    goto exit;
                bestValue = val;
                if (val <= alpha || val >= beta)
                    window *= 2;
                else
                    break;
            }
            rootDepth++;
        }
    exit:
        return bestValue;
    }

    public Move Think(Board board, Timer timer)
    {
        currentBoard = board;
        currentTimer = timer;
        nodes = 0;
        qNodes = 0;
        lookups = 0;
        var val = IterDeepen();
        Console.WriteLine($"Good Val {val} Depth {rootDepth} Lookups {lookups} Searched {nodes} QSearched {qNodes} Time {timer.MillisecondsElapsedThisTurn}ms");
        return TableEntry.move;
    }
}