using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TicTacToeArena.Shared.Models;

public class GameSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..6].ToUpper();
    public string Name { get; set; } = string.Empty;
    public Player? Host { get; set; }
    public Player? Guest { get; set; }
    public CellState[] Board { get; set; } = new CellState[9];
    public string CurrentTurn { get; set; } = string.Empty;
    public GameStatus Status { get; set; } = GameStatus.Waiting;
    public string? Winner { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Helper properties
    public bool IsFull => Host != null && Guest != null;
    public bool IsEmpty => Host == null && Guest == null;
}