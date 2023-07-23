using ChessChallenge.API;
using System.Linq;
using System;
using System.Collections.Generic;

public class MyBot : IChessBot
{
    int[] pieceValues = { 0, 100, 300, 300, 500, 900, 20000 };
    Board currentBoard;

    int evaluate(int meToMove)
    {
        int sum = 0;
        foreach (PieceType type in Enumerable.Range(1, 6))
            sum += pieceValues[(int)type] * (currentBoard.GetPieceList(type, true).Count - currentBoard.GetPieceList(type, false).Count);

        return sum * meToMove;
    }
    int nodes = 0;

    int minimax(int alpha, int beta, int depth, int meToMove, Action<Move> action)
    {
        if (depth == 0) return evaluate(meToMove);
        if (currentBoard.IsDraw()) return 0;
        if (currentBoard.IsInCheckmate()) return -999999;

        var moves = currentBoard.GetLegalMoves();

        foreach (Move move in moves)
        {
            nodes++;
            currentBoard.MakeMove(move);
            int val = -minimax(-beta, -alpha, depth - 1, -meToMove, (move) => { });
            currentBoard.UndoMove(move);
            if (val > alpha)
            {
                alpha = val;
                action(move);
            }
            if (alpha >= beta) return beta;
        }
        return alpha;
    }

    public Move Think(Board board, Timer timer)
    {
        currentBoard = board;
        Move retMove = Move.NullMove;
        nodes = 0;
        var val = minimax(-1073741824, 1073741824, 5, board.IsWhiteToMove ? 1 : -1, (move) => { retMove = move; });
        Console.WriteLine($"Good Val {val} Searched {nodes}");
        return retMove;
    }
}