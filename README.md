# BUPC Codec

A C# entropy encoding/decoding library that builds a **near‑optimal prefix code tree** using dynamic programming and a “fishbone” shape.  
The codec optionally serialises the tree together with the compressed data, making it self‑contained.

## Features

- **Optimal tree construction** – recursive DP chooses the best split points to minimise weighted path length.
- **Fishbone subtrees** – simple degenerate binary trees used when splitting is no longer beneficial.
- **Self‑contained compression** – `EncodingWithTree` / `DecodingWithTree` store the code tree inside the output.
- **Separate tree mode** – share a single `BUPCCodec` instance between encoder and decoder for compact streams.
- **MIT licensed** – free to use in commercial and open‑source projects.

## Algorithm Overview

1. **Frequency analysis** – count occurrence of each byte value.
2. **Sorting** – symbols are ordered by descending frequency.
3. **Dynamic programming** – for each interval `[start, end)` the algorithm compares:
   - Building a simple fishbone tree (depth‑penalty known in closed form).
   - Splitting at some point `k` into two subtrees (`start..k` and `k..end`).
   - The cost is the weighted sum of code lengths.  
   The optimal split (or decision to use a fishbone) is stored.
4. **Tree reconstruction** – the DP tables are used to build a binary tree where leaves hold symbols.
5. **Code generation** – standard tree traversal assigns `0` for left, `1` for right.
6. **Bit packing** – codes are written MSB first into a byte stream, prefixed by the original data length.

## Usage

### Mode 1 – Separate Tree (Encoder/Decoder share same codec instance)

```csharp
using SeanOne.BUPC;

byte[] original = System.Text.Encoding.UTF8.GetBytes("hello world");

// Build optimal codec from data
BUPCCodec codec = BUPCCodec.Create(original);

// Encode (tree not stored in output)
byte[] compressed = codec.Encode(original);

// Decode – requires the exact same codec instance
byte[] decompressed = codec.Decode(compressed);
```

### Mode 2 – Self‑contained (Tree + data in one array)

```csharp
// Encode with embedded tree
byte[] selfContained = BUPCCodec.EncodingWithTree(original);

// Decode – tree is reconstructed automatically
byte[] decompressed = BUPCCodec.DecodingWithTree(selfContained);
```

### Mode 3 – Load a previously serialised tree

```csharp
// Suppose you stored 'serializedTree' from somewhere
byte[] serializedTree = ...; // raw tree bytes (preorder format)
BUPCCodec? codec = BUPCCodec.CreateWithTree(serializedTree);
if (codec != null)
{
    byte[] decoded = codec.Decode(compressedData);
}
```

## API Reference

### `public static BUPCCodec Create(byte[] data)`

Builds an optimal codec based on the frequency distribution of `data`.  
Returns a valid codec even for `null` or empty input (tree becomes `null`).

### `public byte[] Encode(byte[] data)`

Compresses `data` using the codec’s internal tree.  
Output format: `[original length (4 bytes, little‑endian)] [packed bits]`.  
Returns an empty array if input is empty or the tree is missing.

### `public byte[] Decode(byte[] compressed)`

Decompresses data produced by `Encode`.  
Requires that the tree inside the codec matches the one used for encoding.

### `public static byte[] EncodingWithTree(byte[] data)`

Combined encoding: calls `Create(data)` internally, then serialises the tree and appends the compressed data.  
Output format:  
`[tree length (4 bytes)] [tree data (preorder)] [compressed data (original length + bits)]`

### `public static byte[] DecodingWithTree(byte[] combined)`

Decodes data produced by `EncodingWithTree`.  
Extracts the tree, reconstructs the codec, and decodes the remainder.

### `public static BUPCCodec? CreateWithTree(byte[] serializedTree)`

Reconstructs a codec from a previously serialised tree (obtained via `SerializeTree` – note that `SerializeTree` is a private helper, but the format is used internally by `EncodingWithTree`). Useful when you want to store only the tree and later reuse it for decoding multiple streams.

## Serialisation Format (Tree)

- **Preorder traversal**  
  - `0x00` : internal node → then recursively left subtree, then right subtree.  
  - `0x01` : leaf node → followed by one byte (the symbol value).  

This format is used inside `EncodingWithTree` and is recognised by `DecodingWithTree` and `CreateWithTree`.

## Dynamic Programming Details

The algorithm works on the sorted frequency list `w[0..n-1]` (descending).  
For a sub‑interval `[l, r)` (exclusive right bound), the total weight `W = sum(w[l..r-1])`.

- **Fishbone cost** – a tree where symbols are attached along a “spine”:
  - First `(r-l-2)` symbols have depths 1, 2, …, `(r-l-2)`.
  - Last two symbols share depth `(r-l-1)`.
  - Closed‑form computation without building the tree.

- **Split cost** – choose a split point `k`, then `cost(l,k) + cost(k,r) + W`.

The DP chooses the smaller cost. The `dpIsFishbone` flag remembers whether the interval should be a fishbone; `dpSplit` stores the best split point otherwise.

Complexity: O(n³) worst‑case, but for n ≤ 256 (byte alphabet) it is fast enough for practical use.

## Limitations

- The alphabet is limited to 256 distinct byte values.
- The DP does not guarantee a globally optimal Huffman tree (the fishbone restriction is intentional, trading some compression ratio for simpler decoding).
- The codec is **not thread‑safe** for concurrent encoding/decoding (no shared mutable state, but creating multiple instances is fine).
- `Decode` does not validate that the bit stream length matches the original length – it stops when enough symbols have been decoded. Corrupted input may produce shorter or longer output.

## Bit Packing Details

- Bits are packed **MSB first** inside each byte.
- The first code bit goes into the highest bit (bit 7) of the first compressed data byte (offset 4 from the start).
- Unused bits at the end are left as zero and ignored during decoding.

## License

This project is licensed under the **MIT License**.  
You are free to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the software.  
See the [LICENSE](LICENSE) file for details (or include the standard MIT notice in your distribution).
