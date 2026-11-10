using System;
using System.Collections.Generic;

internal enum BoxingCommandType { Strike, Push, GuardStart, GuardEnd }

internal readonly struct BoxingCommand
{
    public readonly BoxingCommandType Type;
    public readonly int Side;
    public readonly PunchKind Kind;
    public readonly BoxingMode Mode;
    public readonly BoxingStance Stance;
    public readonly double Time;

    public BoxingCommand(BoxingCommandType type, int side, PunchKind kind,
        BoxingMode mode, BoxingStance stance, double time)
    {
        Type = type; Side = side; Kind = kind; Mode = mode; Stance = stance; Time = time;
    }
}

// Resolves timestamped press/release events before any animation is started.
// A short pending-click window gives a two-button chord priority over a jab.
internal sealed class BoxingInputBuffer
{
    public double SimultaneousWindow = 0.045;
    public double UppercutWindow = 0.22;
    public double ComboGap = 0.45;
    public double ComboDuration = 0.9;

    private readonly Queue<BoxingCommand> commands = new Queue<BoxingCommand>();
    private bool leftHeld, rightHeld, chordLatched, guarding, hasPending, pendingUppercut;
    private BoxingCommand pending;
    private double uppercutAt = double.NegativeInfinity;
    private int leadCount;
    private double firstLead, lastLead;
    private BoxingStance comboStance;
    private BoxingMode comboMode;

    public void ArmUppercut(double time)
    {
        if (!chordLatched) uppercutAt = time;
    }

    public void Press(int side, double time, BoxingMode mode, BoxingStance stance)
    {
        if (side < 0) leftHeld = true;
        else rightHeld = true;
        if (chordLatched) return;

        bool oppositeHeld = side < 0 ? rightHeld : leftHeld;
        if (hasPending && pending.Side != side && oppositeHeld
            && time - pending.Time <= SimultaneousWindow)
        {
            hasPending = false;
            uppercutAt = double.NegativeInfinity;
            chordLatched = true;
            guarding = mode == BoxingMode.Swing;
            ResetCombo();
            commands.Clear();
            commands.Enqueue(new BoxingCommand(guarding ? BoxingCommandType.GuardStart : BoxingCommandType.Push,
                0, PunchKind.Push, mode, stance, time));
            return;
        }
        if (hasPending) CommitPending();
        hasPending = true;
        pendingUppercut = time >= uppercutAt && time - uppercutAt <= UppercutWindow;
        uppercutAt = double.NegativeInfinity;
        pending = new BoxingCommand(BoxingCommandType.Strike, side, PunchKind.Jab, mode, stance, time);
    }

    public void Release(int side, double time)
    {
        if (side < 0) leftHeld = false;
        else rightHeld = false;
        if (chordLatched)
        {
            // Swing guard persists while either button remains held.
            if (!leftHeld && !rightHeld)
            {
                if (guarding)
                    commands.Enqueue(new BoxingCommand(BoxingCommandType.GuardEnd, 0,
                        PunchKind.Jab, BoxingMode.Swing, default, time));
                chordLatched = guarding = false;
            }
            return;
        }
        // Once released this click cannot be part of a held two-button chord.
        if (hasPending && pending.Side == side) CommitPending();
    }

    public void Advance(double time)
    {
        if (hasPending && time - pending.Time >= SimultaneousWindow) CommitPending();
    }

    public bool TryDequeue(out BoxingCommand command)
    {
        if (commands.Count == 0) { command = default; return false; }
        command = commands.Dequeue();
        return true;
    }

    private void CommitPending()
    {
        if (!hasPending) return;
        hasPending = false;
        PunchKind kind = PunchKind.Jab;
        if (pendingUppercut)
        {
            kind = PunchKind.Uppercut;
            ResetCombo();
        }
        else
        {
            if (pending.Stance != comboStance || pending.Mode != comboMode
                || pending.Time - lastLead > ComboGap || pending.Time - firstLead > ComboDuration)
                ResetCombo();
            comboStance = pending.Stance;
            comboMode = pending.Mode;
            int leadSide = pending.Stance == BoxingStance.Orthodox ? -1 : 1;
            if (pending.Side == leadSide)
            {
                // Keep the most recent two lead taps, so L-L-L-R also uses its last L-L-R.
                if (leadCount == 0) firstLead = pending.Time;
                else if (leadCount >= 2) firstLead = lastLead;
                leadCount = Math.Min(2, leadCount + 1);
                lastLead = pending.Time;
            }
            else
            {
                if (leadCount == 2) kind = PunchKind.Power;
                ResetCombo();
            }
        }
        commands.Enqueue(new BoxingCommand(BoxingCommandType.Strike, pending.Side, kind,
            pending.Mode, pending.Stance, pending.Time));
        pendingUppercut = false;
    }

    public void ResetCombo() => leadCount = 0;

    public void Clear()
    {
        commands.Clear();
        hasPending = pendingUppercut = leftHeld = rightHeld = chordLatched = guarding = false;
        uppercutAt = double.NegativeInfinity;
        ResetCombo();
    }
}
