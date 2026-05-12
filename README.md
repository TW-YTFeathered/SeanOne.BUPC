# BUPC Codec

A simple entropy encoding/decoding implementation in C#. The `BUPCCodec` class builds a custom prefix code tree (fishbone‑shaped) based on symbol frequency, then compresses byte arrays accordingly.

## Overview

- **Encoding**  
  - Analyzes input data to compute symbol frequencies.  
  - Sorts symbols by descending frequency.  
  - Splits the sorted list into left and right halves (balanced by count).  
  - For each half, builds a “fishbone” tree – a degenerate binary tree where one branch always points to a leaf symbol and the other continues the spine.  
  - Traverses the combined virtual root (`LeftSubtree` + `RightSubtree`) to generate binary codes for each byte.  
  - Writes the original data length (4 bytes, little‑endian) followed by the packed bit stream (MSB first).

- **Decoding**  
  - Reads the original length from the header.  
  - Reuses the same tree structure (no tree data stored in the compressed output – the decoder must know or reconstruct the tree!)  
  - Walks the tree bit by bit until a leaf symbol is reached, then repeats from the root.

> ⚠️ **Important**  
> The compressed data produced by `Encode()` does **not** contain the code tree itself.  
> To correctly decode, you must use the same `BUPCCodec` instance (i.e. the same tree) that was used for encoding.  
> In practice this means you need to store or transmit the tree separately, or ensure the sender and receiver agree on the tree building rules.

## Usage

```csharp
using SeanOne.BUPC;

// Original data
byte[] original = System.Text.Encoding.UTF8.GetBytes("hello world");

// Create codec based on the data (tree = f(symbol frequencies))
BUPCCodec codec = BUPCCodec.Create(original);

// Encode
byte[] compressed = codec.Encode(original);

// Decode (using the same codec instance)
byte[] decompressed = codec.Decode(compressed);
```

### Important Notes

- If the input is `null` or empty, `Encode()` returns an empty array and `Decode()` returns an empty array (provided the header indicates length 0).
- The decoder expects the header to contain the **original uncompressed length** as a 32‑bit integer (little‑endian) in the first 4 bytes.
- The codec is stateless after creation, but the tree structure is fixed for a given codec instance. Re‑creating a codec with the same data produces an identical tree because the symbol sorting and splitting are deterministic.

## Algorithm Details

### Tree Construction

1. **Group and sort** – `data.GroupBy(x => x).OrderByDescending(g => g.Count())` → most frequent symbols first.
2. **Split** – `mid = (symbols.Count + 1) / 2`  
   - Left part: first `mid` symbols  
   - Right part: remaining symbols
3. **Build fishbone tree** for each part (parameter `growLeft` controls the direction of spine growth)  
   - For a list of `n` symbols, the tree is a chain of internal nodes with leaf symbols attached alternately.  
   - Example with `growLeft = true` (left child = next spine, right child = leaf symbol).
4. **Virtual root** – combines `LeftSubtree` and `RightSubtree` as its left and right children.

### Bit Packing

- Bits are written in **MSB first** order inside each byte.  
- The first bit of the first code goes to bit 7 of the first compressed data byte (offset 4).  
- Unused trailing bits are simply ignored (they remain zero).

## Limitations

- The codec does **not** handle tree serialization – you must preserve the `BUPCCodec` instance or rebuild it from the same original data.
- Not optimized for very large symbol alphabets (maximum 256 distinct bytes) – the current fishbone tree building is `O(n)` per half.
- No error detection or recovery – if the bit stream is corrupted or the wrong tree is used, decoding will produce garbage.

## API Reference

### `public static BUPCCodec Create(byte[] data)`

Creates a codec whose tree is derived from the frequency distribution of `data`.  
Returns an empty codec (both subtrees `null`) if `data` is `null` or empty.

### `public byte[] Encode(byte[] data)`

Compresses `data` using the tree.  
Returns an array with a 4‑byte length header followed by the packed bits, or an empty array if input is `null`/empty.

### `public byte[] Decode(byte[] compressed)`

Decompresses the given packed data using the same tree.  
Returns the original byte array, or an empty array if the header indicates length 0 or input is invalid.

## License

MIT License
