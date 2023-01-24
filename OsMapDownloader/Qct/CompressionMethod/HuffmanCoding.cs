using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace OsMapDownloader.Qct.CompressionMethod
{
    public class HuffmanCoding : CompressionMethod
    {
        public static byte[] Compress(byte[] source)
        {
            List<byte> compressed = new List<byte>();

            //Build huffman tree
            HuffmanNode huffmanRoot;
            Dictionary<byte, HuffmanColour> paletteToHuffman;
            (huffmanRoot, paletteToHuffman) = BuildHuffmanTree(source);

            //Make 0 be the first byte
            compressed.Add(0x00);

            //Write the codebook
            WriteCodebookRecursive(huffmanRoot, compressed);

            //Write the tile data only if there's more than 1 colour
            List<bool> tileData = new List<bool>();

            if (paletteToHuffman.Count > 1)
            {
                foreach (byte paletteColour in source)
                {
                    List<bool> dataToAdd = new List<bool>();
                    HuffmanNode currentNode = paletteToHuffman[paletteColour];

                    //Build up a list of bits to add
                    while (currentNode != huffmanRoot)
                    {
#pragma warning disable CS8600, CS8602 // Converting null literal or possible null value to non-nullable type.    Dereference of a possibly null reference.
                        HuffmanBranch nextNode = currentNode.Parent;
                        if (nextNode.FalseChild == currentNode)
                            dataToAdd.Add(false);
                        else
                            dataToAdd.Add(true);
#pragma warning restore CS8600, CS8602 // Converting null literal or possible null value to non-nullable type.    Dereference of a possibly null reference.

                        currentNode = nextNode;
                    }

                    //Reverse the list, then add the bits to the tileData
                    dataToAdd.Reverse();
                    tileData.AddRange(dataToAdd);
                }
            }

            //Convert the tile data to a bit array
            BitArray tileDataArray = new BitArray(tileData.ToArray());

            //Convert to byte array and append to compressed data
            byte[] packedTileData = new byte[(int)Math.Ceiling(tileDataArray.Length / 8.0)];
            tileDataArray.CopyTo(packedTileData, 0);
            compressed.AddRange(packedTileData);

            return compressed.ToArray();
        }

        private static void WriteCodebookRecursive(HuffmanNode thisNode, List<byte> compressed)
        {
            //If this node is a colour, just write the colour to the codebook. EZ
            if (thisNode.GetType() == typeof(HuffmanColour))
            {
                HuffmanColour thisNodeC = (HuffmanColour)thisNode;
                compressed.Add(thisNodeC.PaletteIndex);
            }

            //If this node is a branch
            else
            {
                HuffmanBranch thisNodeB = (HuffmanBranch)thisNode;

                //Remember the index of this branch (index of last item in compressed + 1
                int branchIndex = compressed.Count;

                //Recurse false child
                WriteCodebookRecursive(thisNodeB.FalseChild, compressed);

                //Create a branch in the codebook
                int requiredJump = compressed.Count + 1 - branchIndex;

                //Is this a jump we can make with a near instruction?
                if (requiredJump <= 128)
                {
                    compressed.Insert(branchIndex, (byte)(257 - requiredJump));
                }
                //Else, we'll have to use a far instruction
                else
                {
                    //Add 2 to the required jump because now we need 2 extra bytes
                    requiredJump += 2;

                    compressed.Insert(branchIndex, 128);
                    compressed.Insert(branchIndex + 1, (byte)((65537 - requiredJump + 2) % 256));
                    compressed.Insert(branchIndex + 2, (byte)((65537 - requiredJump + 2) / 256));
                }

                //Recurse true child
                WriteCodebookRecursive(thisNodeB.TrueChild, compressed);
            }
        }

        private static (HuffmanNode, Dictionary<byte, HuffmanColour>) BuildHuffmanTree(byte[] source)
        {
            //Build dictionary of colours to frequencies
            Dictionary<byte, int> colourToFrequency = new Dictionary<byte, int>();

            foreach (byte paletteIndex in source)
            {
                if (!colourToFrequency.ContainsKey(paletteIndex))
                {
                    colourToFrequency.Add(paletteIndex, 1);
                }
                else
                {
                    colourToFrequency[paletteIndex]++;
                }
            }

            //Now create and sort a list of these colours as a huffman object
            List<HuffmanNode> nodes = new List<HuffmanNode>();
            Dictionary<byte, HuffmanColour> paletteToHuffman = new Dictionary<byte, HuffmanColour>();
            foreach (KeyValuePair<byte, int> colourFreq in colourToFrequency)
            {
                HuffmanColour nodeToAdd = new HuffmanColour(colourFreq.Key, colourFreq.Value);
                nodes.Add(nodeToAdd);
                paletteToHuffman.Add(colourFreq.Key, nodeToAdd);
            }
            nodes.Sort();

            //Repeat until list length is 1
            while (nodes.Count > 1)
            {
                //Create a new branch from the lowest two valued nodes in the list
                //Put the lower total nodes in the false child to reduce size
                HuffmanBranch newBranch = new HuffmanBranch(
                    nodes[0].TotalNodes <= nodes[1].TotalNodes ? nodes[0] : nodes[1],
                    nodes[0].TotalNodes > nodes[1].TotalNodes ? nodes[0] : nodes[1]);

                //Remove lowest two valued nodes
                nodes.RemoveRange(0, 2);

                //Add new branch to list
                nodes.Add(newBranch);

                //Sort
                nodes.Sort();
            }

            //Return the only node in the list aka the root node
            return (nodes[0], paletteToHuffman);
        }

        private abstract class HuffmanNode : IComparable<HuffmanNode>
        {
            public abstract int NodeValue { get; }
            public abstract int TotalNodes { get; }

            private HuffmanBranch? _parent;
            public HuffmanBranch? Parent
            {
                get { return _parent; }
                set
                {
                    if (value == null) throw new Exception("Parent cannot be set to null");
                    if (value.FalseChild != this && value.TrueChild != this)
                    {
                        throw new Exception("Tried to set a node as a parent which doesn't have this node as one of its children");
                    }

                    _parent = value;
                }
            }

            public int CompareTo([AllowNull] HuffmanNode other)
            {
                return NodeValue.CompareTo(other?.NodeValue);
            }
        }

        private class HuffmanBranch : HuffmanNode
        {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            public HuffmanBranch(HuffmanNode falseChild, HuffmanNode trueChild)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
            {
                FalseChild = falseChild;
                TrueChild = trueChild;
            }

            public override int NodeValue => FalseChild?.NodeValue ?? 0 + TrueChild?.NodeValue ?? 0;
            public override int TotalNodes => FalseChild?.TotalNodes ?? 0 + TrueChild?.TotalNodes ?? 0 + 1;

            private HuffmanNode _falseChild;
            public HuffmanNode FalseChild
            {
                get { return _falseChild; }
                set
                {
                    _falseChild = value;
                    value.Parent = this;
                }
            }

            private HuffmanNode _trueChild;
            public HuffmanNode TrueChild
            {
                get { return _trueChild; }
                set
                {
                    _trueChild = value;
                    value.Parent = this;
                }
            }
        }

        private class HuffmanColour : HuffmanNode
        {
            public override int NodeValue { get; }
            public override int TotalNodes => 1;

            public byte PaletteIndex { get; }

            public HuffmanColour(byte paletteIndex, int occurrences)
            {
                PaletteIndex = paletteIndex;
                NodeValue = occurrences;
            }
        }
    }
}
