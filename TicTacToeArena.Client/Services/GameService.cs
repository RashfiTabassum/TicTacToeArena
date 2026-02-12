using System.Data.Common;
using Microsoft.AspNetCore.SignalR.Client;
using TicTacToeArena.Shared.Models;

namespace TicTacToeArena.Client.Services;

public class GameService
{
    private HubConnection? _hubConnection;
    private readonly string _hubUrl;

    // Events that components can subscribe to
    public event Action<List<GameSession>>? OnSessionListUpdated;
    public event Action<GameSession>? OnGameStarted;
    public event Action<int, string, CellState>? OnMoveMade;
    public event Action<string?, string>? OnGameEnded;
    public event Action<string>? OnError;
    public event Action<GameSession>? OnSessionCreated;

    public Player? CurrentPlayer { get; private set; }
    public GameSession? CurrentSession { get; private set; }

    public CellState[] Board { get; private set; } = new CellState[9];
    public GameService(IConfiguration config)
    {
        _hubUrl = config["ApiUrl"] + "/gamehub";
    }
    public async Task ConnectAsync(string playerName)
    {
        try
        {
            Console.WriteLine($"Connecting to {_hubUrl}");

            // Create the connection
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_hubUrl)
                .WithAutomaticReconnect()
                .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Debug))
                .Build();

            // Wire up server events
            _hubConnection.On<List<GameSession>>("SessionListUpdated",
                sessions =>
                {
                    Console.WriteLine($"Received {sessions.Count} sessions");
                    OnSessionListUpdated?.Invoke(sessions);
                });
            _hubConnection.On<GameSession>("SessionCreated",
                session =>
                {
                    Console.WriteLine($"Session created: {session.Id}");
                    CurrentSession = session; // Set this FIRST
                    OnSessionCreated?.Invoke(session); // Then invoke
                });

            _hubConnection.On<GameSession>("GameStarted",
                session =>
                {
                    Console.WriteLine($"Game started: {session.Id}, Turn: {session.CurrentTurn}");
                    CurrentSession = session;
                    Board = new CellState[9]; // Reset board
                    OnGameStarted?.Invoke(session);
                });

            _hubConnection.On<int, string, CellState>("MoveMade",
                (pos, player, symbol) =>
                {
                    Console.WriteLine($"Move made at {pos} by {player}");
                    Board[pos] = symbol;

                    // Switch turns locally
                    if (CurrentSession != null)
                    {
                        CurrentSession.CurrentTurn = CurrentSession.Host?.Id == player
                            ? CurrentSession.Guest?.Id
                            : CurrentSession.Host?.Id;
                    }

                    OnMoveMade?.Invoke(pos, player, symbol);
            });

            _hubConnection.On<string?, string>("GameEnded",
                (winner, reason) =>
                {
                    Console.WriteLine($"Game ended: {reason}");
                    OnGameEnded?.Invoke(winner, reason);
                });

            _hubConnection.On<string>("Error",
                msg =>
                {
                    Console.WriteLine($"Error from server: {msg}");
                    OnError?.Invoke(msg);
                });

            // Handle connection closed
            _hubConnection.Closed += error =>
            {
                Console.WriteLine($"Connection closed: {error?.Message}");
                return Task.CompletedTask;
            };

            // Start connection
            await _hubConnection.StartAsync();
            Console.WriteLine("Connection started successfully");

            // Register with server
            CurrentPlayer = await _hubConnection.InvokeAsync<Player>("Register", playerName);
            Console.WriteLine($"Registered as: {CurrentPlayer.Name} ({CurrentPlayer.Id})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection error: {ex}");
            throw;
        }
    }

    public async Task CreateSession(string sessionName)
    {
        if (_hubConnection == null || CurrentPlayer == null)
        {
            Console.WriteLine("ERROR: Cannot create session - not connected");
            return;
        }

        Console.WriteLine($"Creating session: {sessionName}");

        // Reset board state
        Board = new CellState[9];

        await _hubConnection.InvokeAsync("CreateSession",
            CurrentPlayer.Id, CurrentPlayer.Name, sessionName);

        Console.WriteLine("CreateSession invoked");
    }

    public async Task JoinSession(string sessionId)
    {
        if (_hubConnection == null || CurrentPlayer == null) return;

        await _hubConnection.InvokeAsync("JoinSession",
            sessionId, CurrentPlayer.Id, CurrentPlayer.Name);
    }

    public async Task MakeMove(int position)
    {
        if (_hubConnection == null || CurrentPlayer == null || CurrentSession == null) return;


        await _hubConnection.InvokeAsync("MakeMove",
            CurrentSession.Id, CurrentPlayer.Id, position);
    }

    public async Task LeaveSession()
    {
        if (_hubConnection == null || CurrentSession == null) return;

        // In a real app, we'd notify the server here
        CurrentSession = null;
    }

    public async Task RequestSessionList()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.InvokeAsync("RequestSessionList");
        }
    }

}