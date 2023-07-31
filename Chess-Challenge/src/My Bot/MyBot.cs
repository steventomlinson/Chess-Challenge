using ChessChallenge.API;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class MyBot : IChessBot
{
    // Debug info
    int evalCalls;
    int nodes;
    int qNodes;

    struct Entry
    {
        public int value = 0;
        public Move move = Move.NullMove;
        public int depth = 0;
        public int type = 0;
        public ulong hash = 0;
        public Entry() { }
    }
    Entry[] table = new Entry[8388608];
    // Entry[] table = new Entry[1024*1024*256];

    readonly int[] pieceValues = { 0, 100, 300, 300, 500, 900, 20000 };
    Board currentBoard;

    int rootDepth;
    int currentDepth;
    Timer currentTimer;

    ulong TableHash => currentBoard.ZobristKey % (ulong)table.LongLength;

    Entry TableEntry => table[TableHash];

    int[] wPawnArr = new int[]{
        0,  0,  0,  0,  0,  0,  0,  0,
        5, 10, 10,-20,-20, 10, 10,  5,
        5, -5,-10,  0,  0,-10, -5,  5,
        0,  0,  0, 20, 20,  0,  0,  0,
        5,  5, 10, 25, 25, 10,  5,  5,
        10, 10, 20, 30, 30, 20, 10, 10,
        50, 50, 50, 50, 50, 50, 50, 50,
        0,  0,  0,  0,  0,  0,  0,  0,
    };

    int Evaluate()
    {
        evalCalls++;
        int sum = 0;
        foreach (PieceList pieceList in currentBoard.GetAllPieceLists())
            foreach (Piece piece in pieceList)
            {
                sum += pieceValues[(int)piece.PieceType] * (piece.IsWhite ? 1 : -1);
                if (piece.PieceType == PieceType.Pawn)
                    if (piece.IsWhite)
                        sum += wPawnArr[(piece.Square.Rank * 8) + piece.Square.File];
                    else
                        sum -= wPawnArr[((7 - piece.Square.Rank) * 8) + piece.Square.File];
            }
        return sum * (currentBoard.IsWhiteToMove ? 1 : -1);
    }


    void Order(Move[] moves)
    {
        var scores = new int[218];
        for (int i = 0; i < moves.Length; i++)
        {
            var move = moves[i];
            int score = 0;
            if (TableEntry.move == move)
            {
                score = 32001;
            }
            if (move.IsCapture)
                score += 10 * pieceValues[(int)move.CapturePieceType] - pieceValues[(int)move.MovePieceType];
            // if (move.IsPromotion)
            //     score += pieceValues[(int)move.PromotionPieceType];

            // if (currentBoard.SquareIsAttackedByOpponent(move.TargetSquare))
            //     score -= pieceValues[(int)move.MovePieceType];

            scores[i] = score;

        }

        for (int i = 0; i < moves.Length - 1; i++)
            for (int j = i + 1; j > 0; j--)
                if (scores[j - 1] < scores[j])
                    (moves[j], moves[j - 1], scores[j], scores[j - 1]) = (moves[j - 1], moves[j], scores[j - 1], scores[j]);
    }

    int GetStoredValue(int alpha, int beta, bool useTable)
    {
        if (useTable && TableEntry.hash == currentBoard.ZobristKey && TableEntry.move != Move.NullMove && currentDepth <= TableEntry.depth)
        {
            if (TableEntry.type == 1)
                return TableEntry.value;
            if (TableEntry.type == 0 && TableEntry.value <= alpha)
                return alpha;
            if(TableEntry.type == 2 && TableEntry.value >= beta)
                return beta;
        }
        return 999999;
    }

    int StoreValue(Move move, int type, int value)
    {
        ref Entry e = ref table[TableHash];
        if (currentDepth >= e.depth)
        {
            e.move = move;
            e.depth = currentDepth;
            e.type = type;
            e.hash = currentBoard.ZobristKey;
            e.value = value;
        }
        return value;
    }

    int Quiesce(int alpha, int beta, bool useTable)
    {
        int bestValue = GetStoredValue(alpha, beta, useTable);
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
            qNodes++;
            currentBoard.MakeMove(move);
            int val = -Quiesce(-beta, -alpha, true);
            currentBoard.UndoMove(move);
            bestValue = Math.Max(val, bestValue);
            if (bestValue >= beta) return beta;
            if (bestValue > alpha)
                alpha = bestValue;
        }
        return alpha;
    }

    int AlphaBetaSearch(int alpha, int beta, bool useTable)
    {
        if (currentBoard.IsDraw()) return 0;
        if (currentBoard.IsInCheckmate()) return -32001 + (rootDepth - currentDepth);
        int bestValue = GetStoredValue(alpha, beta, useTable);
        if (bestValue != 999999)
            return bestValue;

        if (currentDepth == 0) return Quiesce(alpha, beta, useTable);
        // This allows us to always be making progress to checkmate when checkmate is available
        // before we would move back and forth between positions until forced to due to a.

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
            int val;
            if (type == 0)
            {
                val = -AlphaBetaSearch(-beta, -alpha, true);
            }
            else
            {
                val = -AlphaBetaSearch(-alpha - 1, -alpha, true);
                if (val > alpha)
                    val = -AlphaBetaSearch(-beta, -alpha, true);
            }
            currentDepth++;
            currentBoard.UndoMove(move);
            if (currentTimer.MillisecondsElapsedThisTurn > 200)
            {
                if (bestMove != Move.NullMove)
                    return StoreValue(bestMove, type, alpha);
                else
                    return alpha;
            }
            if (val > bestValue)
            {
                bestValue = val;
                bestMove = move;
                if (val > alpha)
                {
                    if (val >= beta)
                    {
                        return StoreValue(move, 2, beta);
                    }
                    alpha = val;
                    type = 1;
                }
            }
        }
        return StoreValue(bestMove, type, alpha);
    }
    int bestValue;
    void IterDeepen()
    {
        int val = -32001;
        rootDepth = 1;
        while (true)
        {
            currentDepth = rootDepth;
            var window = 25;
            while (true)
            {
                var alpha = Math.Max(val - window, -32001);
                var beta = Math.Min(val + window, 32001);
                val = AlphaBetaSearch(alpha, beta, false);
                if (currentTimer.MillisecondsElapsedThisTurn > 200)
                    return;
                bestValue = val;
                if (val <= alpha || val >= beta)
                    window *= 2;
                else
                    break;
            }
            rootDepth++;
        }
    }

    public Move Think(Board board, Timer timer)
    {
        currentBoard = board;
        currentTimer = timer;
        bestValue = 0;
        evalCalls = 0;
        nodes = 0;
        qNodes = 0;
        IterDeepen();
        Console.WriteLine($"Good Val {bestValue} Depth {rootDepth} Eval Calls {evalCalls} Searched {nodes} QSearched {qNodes} Time {timer.MillisecondsElapsedThisTurn}ms");
        return TableEntry.move;
    }
}