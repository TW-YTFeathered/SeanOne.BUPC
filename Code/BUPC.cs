using System;
using System.Collections.Generic;
using System.Linq;

namespace SeanOne.BUPC
{
    public class BUPCCodec
    {
        private class Node
        {
            public byte? Symbol { get; set; }
            public Node Left { get; set; }
            public Node Right { get; set; }
        }

        private Node _root;

        // Cache for dynamic programming
        private byte[] _symbols;
        private int[] _weights;
        private long[] _prefixSum;
        private long[,] _dpCost;
        private int[,] _dpSplit;
        private bool[,] _dpIsFishbone;

        private BUPCCodec() { }

        /// <summary>
        /// Creates an optimal "recursive fishbone" codec from the given data.
        /// Symbols are sorted by descending frequency, then dynamic programming
        /// determines the optimal splits and fishbone terminations.
        /// </summary>
        public static BUPCCodec Create(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return new BUPCCodec { _root = null };
            }

            // Count frequencies and sort in descending order
            var freq = data.GroupBy(b => b)
                           .Select(g => (Symbol: g.Key, Count: g.Count()))
                           .OrderByDescending(p => p.Count)
                           .ToList();

            int n = freq.Count;
            byte[] symbols = new byte[n];
            int[] weights = new int[n];
            for (int i = 0; i < n; i++)
            {
                symbols[i] = freq[i].Symbol;
                weights[i] = freq[i].Count;
            }

            // Prefix sums for fast interval total weight calculation
            long[] prefixSum = new long[n + 1];
            for (int i = 0; i < n; i++)
                prefixSum[i + 1] = prefixSum[i] + weights[i];

            // DP tables
            long[,] dpCost = new long[n + 1, n + 1];
            int[,] dpSplit = new int[n + 1, n + 1];
            bool[,] dpIsFishbone = new bool[n + 1, n + 1];

            // Memoized computation of optimal cost
            ComputeDP(0, n, symbols, weights, prefixSum, dpCost, dpSplit, dpIsFishbone);

            // Reconstruct tree from DP results
            Node root = BuildTree(0, n, symbols, dpSplit, dpIsFishbone);

            return new BUPCCodec
            {
                _root = root,
                _symbols = symbols,
                _weights = weights,
                _prefixSum = prefixSum,
                _dpCost = dpCost,
                _dpSplit = dpSplit,
                _dpIsFishbone = dpIsFishbone
            };
        }

        #region Dynamic Programming Core

        /// <summary>Computes the optimal weighted path cost for interval [start, end) relative to the subtree root.</summary>
        private static long ComputeDP(int start, int end,
                                      byte[] symbols, int[] weights, long[] prefixSum,
                                      long[,] dpCost, int[,] dpSplit, bool[,] dpIsFishbone)
        {
            if (start >= end)
                return 0;
            if (dpCost[start, end] != 0 || dpIsFishbone[start, end]) // Already computed, return cached
                return dpCost[start, end];

            long totalWeight = prefixSum[end] - prefixSum[start];
            int count = end - start;

            // 1. Cost of not splitting, directly generating a simple fishbone
            long fishCost = FishboneCost(start, count, weights);
            long bestCost = fishCost;
            bool isFish = true;
            int bestSplit = -1;

            // 2. Try all split points k, dividing into [start, k) and [k, end)
            for (int k = start + 1; k < end; k++)
            {
                long leftCost = ComputeDP(start, k, symbols, weights, prefixSum, dpCost, dpSplit, dpIsFishbone);
                long rightCost = ComputeDP(k, end, symbols, weights, prefixSum, dpCost, dpSplit, dpIsFishbone);
                long total = leftCost + rightCost + totalWeight;
                if (total < bestCost)
                {
                    bestCost = total;
                    isFish = false;
                    bestSplit = k;
                }
            }

            dpCost[start, end] = bestCost;
            dpSplit[start, end] = bestSplit;
            dpIsFishbone[start, end] = isFish;
            return bestCost;
        }

        /// <summary>Calculates the weighted path length sum of a simple fishbone tree.</summary>
        /// <param name="start">Starting index of symbols</param>
        /// <param name="count">Number of symbols</param>
        /// <param name="weights">Weight array</param>
        private static long FishboneCost(int start, int count, int[] weights)
        {
            if (count == 0) return 0;
            if (count == 1) return 0;
            if (count == 2) // Two leaves at depth 1
                return (long)weights[start] + weights[start + 1];

            long cost = 0;
            // The first count-2 symbols have depths 1, 2, ..., count-2
            for (int i = 0; i < count - 2; i++)
            {
                cost += (long)weights[start + i] * (i + 1);
            }
            // The last two symbols both have depth count-1
            int depth = count - 1;
            cost += (long)weights[start + count - 2] * depth;
            cost += (long)weights[start + count - 1] * depth;
            return cost;
        }

        #endregion

        #region Tree Reconstruction

        /// <summary>Recursively builds the tree from DP results.</summary>
        private static Node BuildTree(int start, int end,
                                      byte[] symbols,
                                      int[,] dpSplit, bool[,] dpIsFishbone)
        {
            if (start >= end) return null;
            if (dpIsFishbone[start, end])
            {
                // Directly build a simple fishbone (direction doesn't affect length, fixed growLeft = true)
                return BuildSimpleFishbone(symbols, start, end, growLeft: true);
            }
            else
            {
                int split = dpSplit[start, end];
                Node leftChild = BuildTree(start, split, symbols, dpSplit, dpIsFishbone);
                Node rightChild = BuildTree(split, end, symbols, dpSplit, dpIsFishbone);
                return new Node { Left = leftChild, Right = rightChild };
            }
        }

        /// <summary>Builds a simple fishbone tree based on index range.</summary>
        private static Node BuildSimpleFishbone(byte[] symbols, int start, int end, bool growLeft)
        {
            int count = end - start;
            if (count == 0) return null;
            if (count == 1) return new Node { Symbol = symbols[start] };

            Node root = new Node();
            Node current = root;

            for (int i = 0; i < count; i++)
            {
                int symIndex = start + i;
                if (i == count - 2)
                {
                    current.Left = new Node { Symbol = symbols[symIndex] };
                    current.Right = new Node { Symbol = symbols[symIndex + 1] };
                    break;
                }
                else
                {
                    Node dataNode = new Node { Symbol = symbols[symIndex] };
                    Node spine = new Node();
                    if (growLeft)
                    {
                        current.Right = dataNode;
                        current.Left = spine;
                    }
                    else
                    {
                        current.Left = dataNode;
                        current.Right = spine;
                    }
                    current = spine;
                }
            }
            return root;
        }

        #endregion

        #region Encoding / Decoding

        public byte[] Encode(byte[] data)
        {
            if (data == null || data.Length == 0) return Array.Empty<byte>();
            if (_root == null) return Array.Empty<byte>();

            var table = BuildEncodingTable();
            int totalBits = data.Sum(b => table[b].Length);
            byte[] lenBytes = BitConverter.GetBytes(data.Length);
            int byteCount = (totalBits + 7) / 8;
            byte[] compressed = new byte[4 + byteCount];
            Array.Copy(lenBytes, 0, compressed, 0, 4);

            int bitIdx = 0;
            foreach (byte b in data)
            {
                string bits = table[b];
                foreach (char c in bits)
                {
                    if (c == '1')
                    {
                        int bytePos = 4 + bitIdx / 8;
                        int bitPos = 7 - (bitIdx % 8);
                        compressed[bytePos] |= (byte)(1 << bitPos);
                    }
                    bitIdx++;
                }
            }
            return compressed;
        }

        public byte[] Decode(byte[] compressed)
        {
            if (compressed == null || compressed.Length < 4) return Array.Empty<byte>();
            int origLen = BitConverter.ToInt32(compressed, 0);
            if (origLen == 0) return Array.Empty<byte>();
            if (_root == null) return Array.Empty<byte>();

            // Fix: if there is no bit data at all, but the root is a single symbol
            if (compressed.Length == 4 && _root.Symbol.HasValue)
            {
                byte singleSymbol = _root.Symbol.Value;
                byte[] result1 = new byte[origLen];
                for (int i = 0; i < origLen; i++)
                    result1[i] = singleSymbol;
                return result1;
            }

            // Normal decoding process
            byte[] result = new byte[origLen];
            Node cur = _root;
            int idx = 0;
            int totalBits = (compressed.Length - 4) * 8;
            int bitPos = 0;
            while (idx < origLen && bitPos < totalBits)
            {
                int bytePos = 4 + bitPos / 8;
                int bitOff = 7 - (bitPos % 8);
                bool bit = (compressed[bytePos] & (1 << bitOff)) != 0;
                cur = bit ? cur.Right : cur.Left;
                if (cur != null && cur.Symbol.HasValue)
                {
                    result[idx++] = cur.Symbol.Value;
                    cur = _root;
                }
                bitPos++;
            }
            return result;
        }

        private Dictionary<byte, string> BuildEncodingTable()
        {
            var table = new Dictionary<byte, string>();
            Traverse(_root, "", table);
            return table;
        }

        private void Traverse(Node node, string prefix, Dictionary<byte, string> table)
        {
            if (node == null) return;
            if (node.Symbol.HasValue)
            {
                // Each symbol has only one leaf, record its shortest path
                table[node.Symbol.Value] = prefix;
                return;
            }
            Traverse(node.Left, prefix + "0", table);
            Traverse(node.Right, prefix + "1", table);
        }

        #endregion
    }
}
