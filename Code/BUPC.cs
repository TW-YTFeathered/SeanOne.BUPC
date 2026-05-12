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

        private BUPCCodec() { }

        private Node LeftSubtree;
        private Node RightSubtree;

        // ==================== 建立編碼樹 ====================

        public static BUPCCodec Create(byte[] data)
        {
            if (data == null || data.Length == 0) return new BUPCCodec();

            var symbols = data.GroupBy(x => x)
                              .OrderByDescending(x => x.Count())
                              .Select(g => g.Key)
                              .ToList();

            int mid = (symbols.Count + 1) / 2;
            var leftPart = symbols.Take(mid).ToList();
            var rightPart = symbols.Skip(mid).ToList();

            return new BUPCCodec
            {
                LeftSubtree = BuildFishboneTree(leftPart, growLeft: true),
                RightSubtree = BuildFishboneTree(rightPart, growLeft: false)
            };
        }

        private static Node BuildFishboneTree(List<byte> symbols, bool growLeft)
        {
            int count = symbols.Count;
            if (count == 0) return null;
            if (count == 1) return new Node { Symbol = symbols[0] };

            Node root = new Node();
            Node current = root;

            for (int i = 0; i < count; i++)
            {
                if (i == count - 2)
                {
                    current.Left = new Node { Symbol = symbols[i] };
                    current.Right = new Node { Symbol = symbols[i + 1] };
                    break;
                }
                else
                {
                    Node dataNode = new Node { Symbol = symbols[i] };
                    Node nextSpine = new Node();

                    if (growLeft)
                    {
                        current.Right = dataNode;
                        current.Left = nextSpine;
                    }
                    else
                    {
                        current.Left = dataNode;
                        current.Right = nextSpine;
                    }
                    current = nextSpine;
                }
            }
            return root;
        }

        // ==================== 編碼 / 解碼 ====================

        public byte[] Encode(byte[] data)
        {
            if (data == null || data.Length == 0) return Array.Empty<byte>();

            var table = BuildEncodingTable();

            // 1) 計算總位元數
            int totalBits = 0;
            foreach (byte b in data)
                totalBits += table[b].Length;

            // 2) 寫入長度標頭（原始資料位元組數）
            byte[] lengthBytes = BitConverter.GetBytes(data.Length);
            int byteCount = (totalBits + 7) / 8;
            byte[] compressed = new byte[4 + byteCount];
            Array.Copy(lengthBytes, 0, compressed, 0, 4);

            // 3) 填入位元（MSB first）
            int bitIndex = 0;
            foreach (byte b in data)
            {
                string code = table[b];
                foreach (char c in code)
                {
                    if (c == '1')
                    {
                        int bytePos = 4 + bitIndex / 8;
                        int bitPos = 7 - (bitIndex % 8);
                        compressed[bytePos] |= (byte)(1 << bitPos);
                    }
                    bitIndex++;
                }
            }
            return compressed;
        }

        public byte[] Decode(byte[] compressed)
        {
            if (compressed == null || compressed.Length < 4)
                return Array.Empty<byte>();

            int originalLength = BitConverter.ToInt32(compressed, 0);
            if (originalLength == 0) return Array.Empty<byte>();

            byte[] result = new byte[originalLength];
            Node root = BuildVirtualRoot();
            Node current = root;
            int symbolIndex = 0;

            int totalBits = (compressed.Length - 4) * 8;
            int bitIndex = 0;

            while (symbolIndex < originalLength && bitIndex < totalBits)
            {
                int bytePos = 4 + bitIndex / 8;
                int bitPos = 7 - (bitIndex % 8);
                bool bit = (compressed[bytePos] & (1 << bitPos)) != 0;

                current = bit ? current.Right : current.Left;

                if (current != null && current.Symbol.HasValue)
                {
                    result[symbolIndex++] = current.Symbol.Value;
                    current = root;
                }
                bitIndex++;
            }
            return result;
        }

        // ==================== 輔助方法 ====================

        private Node BuildVirtualRoot()
        {
            return new Node { Left = LeftSubtree, Right = RightSubtree };
        }

        private Dictionary<byte, string> BuildEncodingTable()
        {
            var table = new Dictionary<byte, string>();
            Traverse(BuildVirtualRoot(), "", table);
            return table;
        }

        private void Traverse(Node node, string prefix, Dictionary<byte, string> table)
        {
            if (node == null) return;
            if (node.Symbol.HasValue)
            {
                table[node.Symbol.Value] = prefix;
                return;
            }
            Traverse(node.Left, prefix + "0", table);
            Traverse(node.Right, prefix + "1", table);
        }
    }
}
