using Godot;
using System;
using System.Linq;
using Snl = Vectordrawing.StringNamesList;
namespace Vectordrawing;

public partial class LetterContainer : PanelContainer
{
    Editor ed = null!;
    VBoxContainer RowContainer = null!; 
    Godot.Collections.Array<Node> Rows = null!;
    Panel Selector = null!;
    int relativeRow = 0;
    int currentColumn = 0;
    int absoluteRow = 0;
    int currentlyVisible = 0;
    const int ROW_AMOUNT = 10;
    public override void _Ready()
    {
        RowContainer = (VBoxContainer)FindChild("RowContainer");
        ed = (Editor)(GetParent().GetParent().GetParent().GetParent().GetParent());
        Rows = RowContainer.GetChildren();
        GetWindow().SizeChanged += AdjustRows;
        Selector = (Panel)(GetParent().GetParent().GetParent().GetParent().FindChild("Selector"));
    }

    public override void _Process(double delta)
    {
        int ymax = currentlyVisible - 1;
        var mov = ed.GetMovementInput((float)delta, wait: 0.010f, OnGuide: (currentColumn == 0 || relativeRow == 0 || currentColumn == 9 || relativeRow == ymax));
        if (mov.X > 0)
        {
            currentColumn += 1;
            if (currentColumn > ROW_AMOUNT - 1)
                currentColumn = 0;
        }
        if (mov.Y > 0)
        {
            absoluteRow += 1;
            if (relativeRow >= ymax)
            {
                if (absoluteRow == Rows.Count)
                {
                    relativeRow = 0;
                    absoluteRow = 0;
                    for (int i = 0; i < currentlyVisible; i++)
                    {
                        ((HBoxContainer)Rows[^(i + 1)]).Visible = false;
                    }
                    for (int i = 0; i < currentlyVisible; i++)
                    {
                        ((HBoxContainer)Rows[i]).Visible = true;
                    }
                }
                else
                {
                    int toMakeInvisible = absoluteRow - currentlyVisible;
                    ((HBoxContainer)Rows[toMakeInvisible]).Visible = false;
                    ((HBoxContainer)Rows[absoluteRow]).Visible = true;
                }
            }
            else
            {
                relativeRow += 1;
            }
        }
        if (mov.X < 0)
        {
            currentColumn -= 1;
            if (currentColumn < 0)
                currentColumn = ROW_AMOUNT - 1;
        }
        if (mov.Y < 0)
        {
            absoluteRow -= 1;
            if (relativeRow == 0)
            {
                if (absoluteRow < 0)
                {
                    relativeRow = currentlyVisible - 1;
                    absoluteRow = Rows.Count - 1;
                    for (int i = 0; i < currentlyVisible; i++)
                    {
                        ((HBoxContainer)Rows[i]).Visible = false;
                    }
                    for (int i = 0; i < currentlyVisible; i++)
                    {
                        ((HBoxContainer)Rows[^(i + 1)]).Visible = true;
                    }

                }
                else
                {
                    int toMakeInvisible = absoluteRow + currentlyVisible;
                    ((HBoxContainer)Rows[toMakeInvisible]).Visible = false;
                    ((HBoxContainer)Rows[absoluteRow]).Visible = true;
                }
            }
            else 
            {
                relativeRow -= 1;
            }
        }
        if (mov.Length() > 0)
        {
            Manager.PlaySound("Rattle3.wav",.15f, 1.0f, 1.5f);
            GD.Print($"{relativeRow} {absoluteRow} {currentlyVisible}");
        }
        Selector.GlobalPosition = Selector.GlobalPosition.Lerp(((GlyphPreview)Rows[absoluteRow].GetChildren()[currentColumn]).GlobalPosition - new Vector2(7,7), 0.2f);
    }

    public void AdjustRows()
    {
        // calculate how many rows will now be visible
        currentlyVisible = (int)MathF.Floor((Size.Y - 40) / 90);

        // Shrinking the window will make the bottom rows invisible
        // move the relative cursor down so it's on a visible row and adjust absoluteRow appropriately
        if (relativeRow > currentlyVisible - 1 && currentlyVisible > 0)
        {
            int decreasing = relativeRow - (currentlyVisible - 1);
            relativeRow -= decreasing;
            absoluteRow -= decreasing;
        }
        foreach (HBoxContainer row in Rows)
        {
            row.Visible = false;
        }
        int firstVisibleRow = absoluteRow - relativeRow;
        // Growing the window will start revealing the amount of rows that SHOULD be visible
        // from the first (visually highest, lowest index) row that's currently visible
        for (int i = firstVisibleRow; i < firstVisibleRow + currentlyVisible; i++)
        {
            int overflow = i - (Rows.Count - 1);
            if (overflow <= 0)
            {
                ((HBoxContainer)Rows[i]).Visible = true;
            }
            // except: if we would start revealing rows past the last row, 
            else
            {
                // start revealing rows above the first visible row instead
                ((HBoxContainer)Rows[firstVisibleRow - overflow]).Visible = true;
                relativeRow += 1;
            }
        }

        /* (20 rows, 0-based indexing, currentlyVisible: 4)

                 ~ 12 more rows ~
        row 12  ...................
        row 13  ...................  
        row 14 [      VISIBLE      ] < The first  visible row: absolute 14, relative 0
        row 15 [      VISIBLE      ] < The second visible row: absolute 15, relative 1
        row 16 [      VISIBLE      ] <
        row 17 [      VISIBLE      ] < start removing rows from here when shrinking
        row 18  ...................  < start adding rows from here when growing
        row 19  ...................  < still growing after this row's been added? Add from row 13 downward
        ~end~

        */
    }
}
