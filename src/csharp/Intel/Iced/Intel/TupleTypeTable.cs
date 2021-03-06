/*
Copyright (C) 2018-2019 de4dot@gmail.com

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY
CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT,
TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

#if DECODER || ENCODER
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Iced.Intel {
	static class TupleTypeTable {
#if HAS_SPAN
		static ReadOnlySpan<byte> tupleTypeData =>
#else
		internal static readonly byte[] tupleTypeData =
#endif
			new byte[] {
				// GENERATOR-BEGIN: TupleTypeTable
				// ⚠️This was generated by GENERATOR!🦹‍♂️
				// TupleType.N1
				0x01,// N
				0x01,// Nbcst
				// TupleType.N2
				0x02,// N
				0x02,// Nbcst
				// TupleType.N4
				0x04,// N
				0x04,// Nbcst
				// TupleType.N8
				0x08,// N
				0x08,// Nbcst
				// TupleType.N16
				0x10,// N
				0x10,// Nbcst
				// TupleType.N32
				0x20,// N
				0x20,// Nbcst
				// TupleType.N64
				0x40,// N
				0x40,// Nbcst
				// TupleType.N8b4
				0x08,// N
				0x04,// Nbcst
				// TupleType.N16b4
				0x10,// N
				0x04,// Nbcst
				// TupleType.N32b4
				0x20,// N
				0x04,// Nbcst
				// TupleType.N64b4
				0x40,// N
				0x04,// Nbcst
				// TupleType.N16b8
				0x10,// N
				0x08,// Nbcst
				// TupleType.N32b8
				0x20,// N
				0x08,// Nbcst
				// TupleType.N64b8
				0x40,// N
				0x08,// Nbcst
				// GENERATOR-END: TupleTypeTable
			};

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint GetDisp8N(TupleType tupleType, bool bcst) {
			int index = ((int)tupleType << 1) | (bcst ? 1 : 0);
			Debug.Assert((uint)index < (uint)tupleTypeData.Length);
			return tupleTypeData[index];
		}
	}
}
#endif
