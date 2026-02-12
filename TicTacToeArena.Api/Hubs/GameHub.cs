using Microsoft.AspNetCore.SignalR;
using TicTacToeArena.Shared.Models;

namespace TicTacToeArena.Api.Hubs;

public class GameHub : Hub
{
    // We'll add methods here step by step
    // This stores active sessions in memory (we'll use Dictionary like a phone book)
    private static readonly Dictionary<string, GameSession> _sessions = new();
    private static readonly Dictionary<string, string> _playerSessions = new(); // ConnectionId -> SessionId

    public async Task<Player> Register(string name)
    {
        var player = new Player
        {
            Name = name,
            ConnectionId = Context.ConnectionId
        };

        // Send the list of available games to this new player
        await Clients.Caller.SendAsync("SessionListUpdated", GetAvailableSessions());

        return player;
    }

    private List<GameSession> GetAvailableSessions()
    {
        var all = _sessions.Values.ToList();
        Console.WriteLine($"[SERVER] GetAvailableSessions - Total: {all.Count}");

        foreach (var s in all)
        {
            Console.WriteLine($"[SERVER]   Session {s.Id}: Status={s.Status}, IsFull={s.IsFull}, Host={s.Host?.Name}, Guest={s.Guest?.Name}");
        }

        var available = all
            .Where(s => s.Status == GameStatus.Waiting && !s.IsFull)
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        Console.WriteLine($"[SERVER] Returning {available.Count} available sessions");
        return available;
    }

    public async Task<GameSession> CreateSession(string playerId, string playerName, string sessionName)
    {
        var session = new GameSession
        {
            Name = string.IsNullOrWhiteSpace(sessionName) ? $"{playerName}'s Game" : sessionName,
            Host = new Player
            {
                Id = playerId,
                Name = playerName,
                ConnectionId = Context.ConnectionId
            }
        };

        Console.WriteLine($"[SERVER] Creating session {session.Id} - Status: {session.Status}");

        // Store the session
        _sessions[session.Id] = session;
        _playerSessions[Context.ConnectionId] = session.Id;

        Console.WriteLine($"[SERVER] Total sessions in memory: {_sessions.Count}");

        // Add this connection to a SignalR "group" (so we can message both players easily)
        await Groups.AddToGroupAsync(Context.ConnectionId, session.Id);

        // Tell the creator their session is ready
        await Clients.Caller.SendAsync("SessionCreated", session);

        // Tell everyone else there's a new session available
        var availableSessions = GetAvailableSessions();
        Console.WriteLine($"[SERVER] Broadcasting {availableSessions.Count} available sessions to all clients");
        await Clients.All.SendAsync("SessionListUpdated", availableSessions);

        return session;
    }

    public async Task<GameSession?> JoinSession(string sessionId, string playerId, string playerName)
    {
        // Check if session exists
        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            await Clients.Caller.SendAsync("Error", "Session not found");
            return null;
        }

        // Check if full
        if (session.IsFull)
        {
            await Clients.Caller.SendAsync("Error", "Session is full");
            return null;
        }

        // Add guest
        session.Guest = new Player
        {
            Id = playerId,
            Name = playerName,
            ConnectionId = Context.ConnectionId
        };

        session.Status = GameStatus.Playing;
        session.CurrentTurn = session.Host!.Id; // Host goes first

        // Track this player
        _playerSessions[Context.ConnectionId] = sessionId;
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);

        // Notify BOTH players that game starts
        await Clients.Group(session.Id).SendAsync("GameStarted", session);

        // Update lobby for everyone else (this session is now full)
        await Clients.All.SendAsync("SessionListUpdated", GetAvailableSessions());

        return session;
    }

    public async Task MakeMove(string sessionId, string playerId, int position)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return;

        // Validation
        if (session.Status != GameStatus.Playing)
        {
            await Clients.Caller.SendAsync("Error", "Game not in progress");
            return;
        }

        if (session.CurrentTurn != playerId)
        {
            await Clients.Caller.SendAsync("Error", "Not your turn");
            return;
        }

        if (position < 0 || position > 8 || session.Board[position] != CellState.Empty)
        {
            await Clients.Caller.SendAsync("Error", "Invalid move");
            return;
        }

        // Execute move
        var symbol = session.Host!.Id == playerId ? CellState.X : CellState.O;
        session.Board[position] = symbol;

        // Broadcast to both players
        await Clients.Group(sessionId).SendAsync("MoveMade", position, playerId, symbol);

        // Check for win or draw
        if (CheckWin(session.Board, out var winner))
        {
            session.Status = GameStatus.Finished;
            session.Winner = winner == CellState.X ? session.Host.Id : session.Guest!.Id;
            await Clients.Group(sessionId).SendAsync("GameEnded", session.Winner, "win");
        }
        else if (session.Board.All(c => c != CellState.Empty))
        {
            session.Status = GameStatus.Finished;
            await Clients.Group(sessionId).SendAsync("GameEnded", null, "draw");
        }
        else
        {
            // Switch turns
            session.CurrentTurn = session.CurrentTurn == session.Host.Id
                ? session.Guest!.Id
                : session.Host.Id;
        }
    }

    private bool CheckWin(CellState[] board, out CellState winner)
    {
        // All winning lines: rows, columns, diagonals
        int[][] lines = new[]
        {
        new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, // Rows
        new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 }, // Columns
        new[] { 0, 4, 8 }, new[] { 2, 4, 6 }                     // Diagonals
    };

        foreach (var line in lines)
        {
            var a = board[line[0]];
            var b = board[line[1]];
            var c = board[line[2]];

            if (a != CellState.Empty && a == b && b == c)
            {
                winner = a;
                return true;
            }
        }

        winner = CellState.Empty;
        return false;
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (_playerSessions.TryGetValue(Context.ConnectionId, out var sessionId))
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                // End the game
                session.Status = GameStatus.Abandoned;
                await Clients.Group(sessionId).SendAsync("GameEnded", null, "opponent_left");

                _sessions.Remove(sessionId);
            }
            _playerSessions.Remove(Context.ConnectionId);

            // Update lobby
            await Clients.All.SendAsync("SessionListUpdated", GetAvailableSessions());
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task RequestSessionList()
{
    var sessions = GetAvailableSessions();
    await Clients.Caller.SendAsync("SessionListUpdated", sessions);
}


}