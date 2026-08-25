using System;
using System.Collections.Generic;

public sealed class CardDeck
{
    private readonly List<CardData> allCards;
    private readonly List<CardData> drawPile = new();
    private readonly Random random;

    public int CardCount => allCards.Count;
    public int RemainingCount => drawPile.Count;

    public CardDeck(IReadOnlyList<CardData> cards, int? randomSeed = null)
    {
        allCards = cards == null ? new List<CardData>() : new List<CardData>(cards);
        random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
        RefillAndShuffle();
    }

    public List<CardData> DrawUniqueHand(int requestedCount)
    {
        int targetCount = Math.Min(Math.Max(0, requestedCount), allCards.Count);
        List<CardData> hand = new(targetCount);
        HashSet<string> handIndices = new(StringComparer.Ordinal);

        while (hand.Count < targetCount)
        {
            if (drawPile.Count == 0)
            {
                RefillAndShuffle();
            }

            int candidateIndex = FindCandidateIndex(handIndices);
            if (candidateIndex < 0)
            {
                break;
            }

            CardData card = drawPile[candidateIndex];
            drawPile.RemoveAt(candidateIndex);
            hand.Add(card);
            handIndices.Add(card.Index);
        }

        return hand;
    }

    private int FindCandidateIndex(HashSet<string> excludedIndices)
    {
        for (int i = drawPile.Count - 1; i >= 0; i--)
        {
            if (!excludedIndices.Contains(drawPile[i].Index))
            {
                return i;
            }
        }

        return -1;
    }

    private void RefillAndShuffle()
    {
        drawPile.Clear();
        drawPile.AddRange(allCards);
        for (int i = drawPile.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (drawPile[i], drawPile[swapIndex]) = (drawPile[swapIndex], drawPile[i]);
        }
    }
}
