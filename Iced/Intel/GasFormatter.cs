﻿/*
    Copyright (C) 2018 de4dot@gmail.com

    This file is part of Iced.

    Iced is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Iced is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with Iced.  If not, see <https://www.gnu.org/licenses/>.
*/

#if !NO_GAS_FORMATTER && !NO_FORMATTER
using System;
using System.Diagnostics;
using Iced.Intel.GasFormatterInternal;

namespace Iced.Intel {
	/// <summary>
	/// GNU assembler (AT&amp;T) formatter options
	/// </summary>
	public sealed class GasFormatterOptions : FormatterOptions {
		/// <summary>
		/// If true, the formatter doesn't add '%' to registers, eg. %eax vs eax
		/// </summary>
		public bool NakedRegisters { get; set; }

		/// <summary>
		/// Shows the mnemonic size suffix, eg. 'mov %eax,%ecx' vs 'movl %eax,%ecx'
		/// </summary>
		public bool ShowMnemonicSizeSuffix { get; set; }

		/// <summary>
		/// Add a space after the comma if it's a memory operand, eg. '(%eax,%ecx,2)' vs '(%eax, %ecx, 2)'
		/// </summary>
		public bool SpaceAfterMemoryOperandComma { get; set; }

		/// <summary>
		/// Constructor
		/// </summary>
		public GasFormatterOptions() {
			HexPrefix = "0x";
			OctalPrefix = "0";
			BinaryPrefix = "0b";
		}
	}

	/// <summary>
	/// GNU assembler (AT&amp;T) formatter
	/// </summary>
	public sealed class GasFormatter : Formatter {
		/// <summary>
		/// Gets the formatter options, see also <see cref="GasOptions"/>
		/// </summary>
		public override FormatterOptions Options => options;

		/// <summary>
		/// Gets the GAS formatter options
		/// </summary>
		public GasFormatterOptions GasOptions => options;

		const string ImmediateValuePrefix = "$";
		readonly GasFormatterOptions options;
		readonly SymbolResolver symbolResolver;
		readonly string[] allRegisters;
		readonly string[] allRegistersNaked;
		readonly InstrInfo[] instrInfos;
		readonly (MemorySize memorySize, string bcstTo)[] allMemorySizes;
		readonly NumberFormatter numberFormatter;

		string[] AllRegisters => options.NakedRegisters ? allRegistersNaked : allRegisters;

		/// <summary>
		/// Constructor
		/// </summary>
		public GasFormatter() : this(new GasFormatterOptions()) { }

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="options">Formatter options</param>
		/// <param name="symbolResolver">Symbol resolver or null</param>
		public GasFormatter(GasFormatterOptions options, SymbolResolver symbolResolver = null) {
			this.options = options ?? throw new ArgumentNullException(nameof(options));
			this.symbolResolver = symbolResolver;
			allRegisters = Registers.AllRegisters;
			allRegistersNaked = Registers.AllRegistersNaked;
			instrInfos = InstrInfos.AllInfos;
			allMemorySizes = MemorySizes.AllMemorySizes;
			numberFormatter = new NumberFormatter(options);
		}

		/// <summary>
		/// Formats the mnemonic and any prefixes
		/// </summary>
		/// <param name="instruction">Instruction</param>
		/// <param name="output">Output</param>
		public override void FormatMnemonic(ref Instruction instruction, FormatterOutput output) {
			Debug.Assert((uint)instruction.Code < (uint)instrInfos.Length);
			var instrInfo = instrInfos[(int)instruction.Code];
			instrInfo.GetOpInfo(options, ref instruction, out var opInfo);
			int column = 0;
			FormatMnemonic(ref instruction, output, ref opInfo, ref column);
		}

		/// <summary>
		/// Gets the number of operands that will be formatted. A formatter can add and remove operands
		/// </summary>
		/// <param name="instruction">Instruction</param>
		/// <returns></returns>
		public override int GetOperandCount(ref Instruction instruction) {
			Debug.Assert((uint)instruction.Code < (uint)instrInfos.Length);
			var instrInfo = instrInfos[(int)instruction.Code];
			instrInfo.GetOpInfo(options, ref instruction, out var opInfo);
			return opInfo.OpCount;
		}

		/// <summary>
		/// Formats an operand. This is a formatter operand and not necessarily a real instruction operand.
		/// A formatter can add and remove operands.
		/// </summary>
		/// <param name="instruction">Instruction</param>
		/// <param name="output">Output</param>
		/// <param name="operand">Operand number, 0-based. This is a formatter operand and isn't necessarily the same as an instruction operand.
		/// See <see cref="GetOperandCount(ref Instruction)"/></param>
		public override void FormatOperand(ref Instruction instruction, FormatterOutput output, int operand) {
			Debug.Assert((uint)instruction.Code < (uint)instrInfos.Length);
			var instrInfo = instrInfos[(int)instruction.Code];
			instrInfo.GetOpInfo(options, ref instruction, out var opInfo);

			if ((uint)operand >= (uint)opInfo.OpCount)
				throw new ArgumentOutOfRangeException(nameof(operand));
			FormatOperand(ref instruction, output, ref opInfo, operand);
		}

		/// <summary>
		/// Formats an operand separator
		/// </summary>
		/// <param name="instruction">Instruction</param>
		/// <param name="output">Output</param>
		public override void FormatOperandSeparator(ref Instruction instruction, FormatterOutput output) {
			output.Write(",", FormatterOutputTextKind.Punctuation);
			if (options.SpaceAfterOperandSeparator)
				output.Write(" ", FormatterOutputTextKind.Text);
		}

		/// <summary>
		/// Formats all operands
		/// </summary>
		/// <param name="instruction">Instruction</param>
		/// <param name="output">Output</param>
		public override void FormatAllOperands(ref Instruction instruction, FormatterOutput output) {
			Debug.Assert((uint)instruction.Code < (uint)instrInfos.Length);
			var instrInfo = instrInfos[(int)instruction.Code];
			instrInfo.GetOpInfo(options, ref instruction, out var opInfo);
			FormatOperands(ref instruction, output, ref opInfo);
		}

		/// <summary>
		/// Formats the whole instruction: prefixes, mnemonic, operands
		/// </summary>
		/// <param name="instruction">Instruction</param>
		/// <param name="output">Output</param>
		public override void Format(ref Instruction instruction, FormatterOutput output) {
			Debug.Assert((uint)instruction.Code < (uint)instrInfos.Length);
			var instrInfo = instrInfos[(int)instruction.Code];
			instrInfo.GetOpInfo(options, ref instruction, out var opInfo);

			int column = 0;
			FormatMnemonic(ref instruction, output, ref opInfo, ref column);

			if (opInfo.OpCount != 0) {
				FormatterUtils.AddTabs(output, column, options.FirstOperandCharIndex, options.TabSize);
				FormatOperands(ref instruction, output, ref opInfo);
			}
		}

		static readonly string[] opSizeStrings = new string[(int)InstrOpInfoFlags.SizeOverrideMask + 1] { null, "data16", "data32", "rex.w" };
		static readonly string[] addrSizeStrings = new string[(int)InstrOpInfoFlags.SizeOverrideMask + 1] { null, "addr16", "addr32", "addr64" };
		void FormatMnemonic(ref Instruction instruction, FormatterOutput output, ref InstrOpInfo opInfo, ref int column) {
			string prefix;

			if ((opInfo.Flags & InstrOpInfoFlags.OpSizeIsByteDirective) != 0) {
				switch ((SizeOverride)(((int)opInfo.Flags >> (int)InstrOpInfoFlags.OpSizeShift) & (int)InstrOpInfoFlags.SizeOverrideMask)) {
				case SizeOverride.None:
					break;

				case SizeOverride.Size16:
				case SizeOverride.Size32:
					var byteDirective = ".byte";
					if (options.UpperCaseKeywords || options.UpperCaseAll)
						byteDirective = byteDirective.ToUpperInvariant();
					output.Write(byteDirective, FormatterOutputTextKind.Directive);
					output.Write(" ", FormatterOutputTextKind.Text);
					var numberOptions = new NumberFormattingOptions(options, options.ShortNumbers, options.SignedImmediateOperands, false);
					var s = numberFormatter.FormatUInt8(ref numberOptions, 0x66);
					output.Write(s, FormatterOutputTextKind.Number);
					output.Write(";", FormatterOutputTextKind.Punctuation);
					output.Write(" ", FormatterOutputTextKind.Text);
					column += byteDirective.Length + 1 + s.Length + 1 + 1;
					break;

				case SizeOverride.Size64:
					FormatPrefix(output, ref column, "rex.w");
					break;

				default:
					throw new InvalidOperationException();
				}
			}
			else {
				prefix = opSizeStrings[((int)opInfo.Flags >> (int)InstrOpInfoFlags.OpSizeShift) & (int)InstrOpInfoFlags.SizeOverrideMask];
				if (prefix != null)
					FormatPrefix(output, ref column, prefix);
			}

			prefix = addrSizeStrings[((int)opInfo.Flags >> (int)InstrOpInfoFlags.AddrSizeShift) & (int)InstrOpInfoFlags.SizeOverrideMask];
			if (prefix != null)
				FormatPrefix(output, ref column, prefix);

			var prefixSeg = instruction.PrefixSegment;
			if (prefixSeg != Register.None && ShowSegmentOverridePrefix(ref opInfo))
				FormatPrefix(output, ref column, allRegistersNaked[(int)prefixSeg]);

			if (instruction.HasPrefixXacquire)
				FormatPrefix(output, ref column, "xacquire");
			if (instruction.HasPrefixXrelease)
				FormatPrefix(output, ref column, "xrelease");
			if (instruction.HasPrefixLock)
				FormatPrefix(output, ref column, "lock");

			bool hasBnd = (opInfo.Flags & InstrOpInfoFlags.BndPrefix) != 0;
			if (instruction.HasPrefixRepe)
				FormatPrefix(output, ref column, FormatterUtils.IsRepeOrRepneInstruction(instruction.Code) ? "repe" : "rep");
			if (instruction.HasPrefixRepne && !hasBnd)
				FormatPrefix(output, ref column, "repne");

			if (hasBnd)
				FormatPrefix(output, ref column, "bnd");

			var mnemonic = opInfo.Mnemonic;
			if (options.UpperCaseMnemonics || options.UpperCaseAll)
				mnemonic = mnemonic.ToUpperInvariant();
			output.Write(mnemonic, FormatterOutputTextKind.Mnemonic);
			column += mnemonic.Length;
			if ((opInfo.Flags & InstrOpInfoFlags.JccNotTaken) != 0)
				FormatBranchHint(output, ref column, "pn");
			else if ((opInfo.Flags & InstrOpInfoFlags.JccTaken) != 0)
				FormatBranchHint(output, ref column, "pt");
		}

		void FormatBranchHint(FormatterOutput output, ref int column, string keyword) {
			output.Write(",", FormatterOutputTextKind.Text);
			if (options.UpperCaseKeywords || options.UpperCaseAll)
				keyword = keyword.ToUpperInvariant();
			output.Write(keyword, FormatterOutputTextKind.Keyword);
			column += 1 + keyword.Length;
		}

		bool ShowSegmentOverridePrefix(ref InstrOpInfo opInfo) {
			if ((opInfo.Flags & (InstrOpInfoFlags.JccNotTaken | InstrOpInfoFlags.JccTaken)) != 0)
				return false;
			for (int i = 0; i < opInfo.OpCount; i++) {
				switch (opInfo.GetOpKind(i)) {
				case InstrOpKind.Register:
				case InstrOpKind.NearBranch16:
				case InstrOpKind.NearBranch32:
				case InstrOpKind.NearBranch64:
				case InstrOpKind.FarBranch16:
				case InstrOpKind.FarBranch32:
				case InstrOpKind.Immediate8:
				case InstrOpKind.Immediate8_2nd:
				case InstrOpKind.Immediate16:
				case InstrOpKind.Immediate32:
				case InstrOpKind.Immediate64:
				case InstrOpKind.Immediate8to16:
				case InstrOpKind.Immediate8to32:
				case InstrOpKind.Immediate8to64:
				case InstrOpKind.Immediate32to64:
				case InstrOpKind.MemoryESDI:
				case InstrOpKind.MemoryESEDI:
				case InstrOpKind.MemoryESRDI:
				case InstrOpKind.Sae:
				case InstrOpKind.RnSae:
				case InstrOpKind.RdSae:
				case InstrOpKind.RuSae:
				case InstrOpKind.RzSae:
					break;

				case InstrOpKind.MemorySegSI:
				case InstrOpKind.MemorySegESI:
				case InstrOpKind.MemorySegRSI:
				case InstrOpKind.MemorySegDI:
				case InstrOpKind.MemorySegEDI:
				case InstrOpKind.MemorySegRDI:
				case InstrOpKind.Memory64:
				case InstrOpKind.Memory:
					return false;

				default:
					throw new InvalidOperationException();
				}
			}
			return true;
		}

		void FormatPrefix(FormatterOutput output, ref int column, string prefix) {
			if (options.UpperCasePrefixes || options.UpperCaseAll)
				prefix = prefix.ToUpperInvariant();
			output.Write(prefix, FormatterOutputTextKind.Prefix);
			output.Write(" ", FormatterOutputTextKind.Text);
			column += prefix.Length + 1;
		}

		void FormatOperands(ref Instruction instruction, FormatterOutput output, ref InstrOpInfo opInfo) {
			for (int i = 0; i < opInfo.OpCount; i++) {
				if (i > 0) {
					output.Write(",", FormatterOutputTextKind.Punctuation);
					if (options.SpaceAfterOperandSeparator)
						output.Write(" ", FormatterOutputTextKind.Text);
				}
				FormatOperand(ref instruction, output, ref opInfo, i);
			}
		}

		void FormatOperand(ref Instruction instruction, FormatterOutput output, ref InstrOpInfo opInfo, int operand) {
			Debug.Assert((uint)operand < (uint)opInfo.OpCount);

			output.OnOperand(operand, begin: true);

			if ((opInfo.Flags & InstrOpInfoFlags.IndirectOperand) != 0)
				output.Write("*", FormatterOutputTextKind.Operator);

			string s;
			FormatterFlowControl flowControl;
			byte imm8;
			ushort imm16;
			uint imm32;
			ulong imm64;
			int immSize;
			bool showBranchSize;
			NumberFormattingOptions numberOptions;
			SymbolResult symbol, symbolSelector;
			SymbolResolver symbolResolver;
			var opKind = opInfo.GetOpKind(operand);
			switch (opKind) {
			case InstrOpKind.Register:
				FormatRegister(output, opInfo.GetOpRegister(operand));
				break;

			case InstrOpKind.NearBranch16:
			case InstrOpKind.NearBranch32:
			case InstrOpKind.NearBranch64:
				if (opKind == InstrOpKind.NearBranch64) {
					immSize = 8;
					imm64 = instruction.NearBranch64Target;
				}
				else if (opKind == InstrOpKind.NearBranch32) {
					immSize = 4;
					imm64 = instruction.NearBranch32Target;
				}
				else {
					immSize = 2;
					imm64 = instruction.NearBranch16Target;
				}
				numberOptions = new NumberFormattingOptions(options, options.ShortBranchNumbers, false, false);
				showBranchSize = options.ShowBranchSize;
				if ((symbolResolver = this.symbolResolver) != null && symbolResolver.TryGetBranchSymbol(operand, ref instruction, imm64, immSize, out symbol, ref showBranchSize, ref numberOptions))
					output.Write(symbol);
				else {
					flowControl = FormatterUtils.GetFlowControl(ref instruction);
					if (opKind == InstrOpKind.NearBranch32)
						s = numberFormatter.FormatUInt32(ref numberOptions, instruction.NearBranch32Target, numberOptions.ShortNumbers);
					else if (opKind == InstrOpKind.NearBranch64)
						s = numberFormatter.FormatUInt64(ref numberOptions, instruction.NearBranch64Target, numberOptions.ShortNumbers);
					else
						s = numberFormatter.FormatUInt16(ref numberOptions, instruction.NearBranch16Target, numberOptions.ShortNumbers);
					output.Write(s, FormatterUtils.IsCall(flowControl) ? FormatterOutputTextKind.FunctionAddress : FormatterOutputTextKind.LabelAddress);
				}
				break;

			case InstrOpKind.FarBranch16:
			case InstrOpKind.FarBranch32:
				if (opKind == InstrOpKind.FarBranch32) {
					immSize = 4;
					imm64 = instruction.FarBranch32Target;
				}
				else {
					immSize = 2;
					imm64 = instruction.FarBranch16Target;
				}
				numberOptions = new NumberFormattingOptions(options, options.ShortBranchNumbers, false, false);
				showBranchSize = options.ShowBranchSize;
				if ((symbolResolver = this.symbolResolver) != null && symbolResolver.TryGetFarBranchSymbol(operand, ref instruction, instruction.FarBranchSelector, (uint)imm64, immSize, out symbolSelector, out symbol, ref showBranchSize, ref numberOptions)) {
					output.Write(ImmediateValuePrefix, FormatterOutputTextKind.Operator);
					if (symbolSelector.Text.IsDefault) {
						s = numberFormatter.FormatUInt16(ref numberOptions, instruction.FarBranchSelector, numberOptions.ShortNumbers);
						output.Write(s, FormatterOutputTextKind.SelectorValue);
					}
					else
						output.Write(symbolSelector);
					output.Write(",", FormatterOutputTextKind.Punctuation);
					if (options.SpaceAfterOperandSeparator)
						output.Write(" ", FormatterOutputTextKind.Text);
					output.Write(ImmediateValuePrefix, FormatterOutputTextKind.Operator);
					output.Write(symbol);
				}
				else {
					flowControl = FormatterUtils.GetFlowControl(ref instruction);
					s = numberFormatter.FormatUInt16(ref numberOptions, instruction.FarBranchSelector, numberOptions.ShortNumbers);
					output.Write(ImmediateValuePrefix, FormatterOutputTextKind.Operator);
					output.Write(s, FormatterOutputTextKind.SelectorValue);
					output.Write(",", FormatterOutputTextKind.Punctuation);
					if (options.SpaceAfterOperandSeparator)
						output.Write(" ", FormatterOutputTextKind.Text);
					if (opKind == InstrOpKind.FarBranch32)
						s = numberFormatter.FormatUInt32(ref numberOptions, instruction.FarBranch32Target, numberOptions.ShortNumbers);
					else
						s = numberFormatter.FormatUInt16(ref numberOptions, instruction.FarBranch16Target, numberOptions.ShortNumbers);
					output.Write(ImmediateValuePrefix, FormatterOutputTextKind.Operator);
					output.Write(s, FormatterUtils.IsCall(flowControl) ? FormatterOutputTextKind.FunctionAddress : FormatterOutputTextKind.LabelAddress);
				}
				break;

			case InstrOpKind.Immediate8:
			case InstrOpKind.Immediate8_2nd:
				output.Write(ImmediateValuePrefix, FormatterOutputTextKind.Operator);
				if (opKind == InstrOpKind.Immediate8)
					imm8 = instruction.Immediate8;
				else
					imm8 = instruction.Immediate8_2nd;
				numberOptions = new NumberFormattingOptions(options, options.ShortNumbers, options.SignedImmediateOperands, false);
				if ((symbolResolver = this.symbolResolver) != null && symbolResolver.TryGetImmediateSymbol(operand, ref instruction, imm8, 1, out symbol, ref numberOptions))
					output.Write(symbol);
				else {
					if (numberOptions.SignedNumber && (sbyte)imm8 < 0) {
						output.Write("-", FormatterOutputTextKind.Operator);
						imm8 = (byte)-(sbyte)imm8;
					}
					s = numberFormatter.FormatUInt8(ref numberOptions, imm8);
					output.Write(s, FormatterOutputTextKind.Number);
				}
				break;

			case InstrOpKind.Immediate16:
			case InstrOpKind.Immediate8to16:
				output.Write(ImmediateValuePrefix, FormatterOutputTextKind.Operator);
				if (opKind == InstrOpKind.Immediate16)
					imm16 = instruction.Immediate16;
				else
					imm16 = (ushort)instruction.Immediate8to16;
				numberOptions = new NumberFormattingOptions(options, options.ShortNumbers, options.SignedImmediateOperands, false);
				if ((symbolResolver = this.symbolResolver) != null && symbolResolver.TryGetImmediateSymbol(operand, ref instruction, imm16, 2, out symbol, ref numberOptions))
					output.Write(symbol);
				else {
					if (numberOptions.SignedNumber && (short)imm16 < 0) {
						output.Write("-", FormatterOutputTextKind.Operator);
						imm16 = (ushort)-(short)imm16;
					}
					s = numberFormatter.FormatUInt16(ref numberOptions, imm16);
					output.Write(s, FormatterOutputTextKind.Number);
				}
				break;

			case InstrOpKind.Immediate32:
			case InstrOpKind.Immediate8to32:
				output.Write(ImmediateValuePrefix, FormatterOutputTextKind.Operator);
				if (opKind == InstrOpKind.Immediate32)
					imm32 = instruction.Immediate32;
				else
					imm32 = (uint)instruction.Immediate8to32;
				numberOptions = new NumberFormattingOptions(options, options.ShortNumbers, options.SignedImmediateOperands, false);
				if ((symbolResolver = this.symbolResolver) != null && symbolResolver.TryGetImmediateSymbol(operand, ref instruction, imm32, 4, out symbol, ref numberOptions))
					output.Write(symbol);
				else {
					if (numberOptions.SignedNumber && (int)imm32 < 0) {
						output.Write("-", FormatterOutputTextKind.Operator);
						imm32 = (uint)-(int)imm32;
					}
					s = numberFormatter.FormatUInt32(ref numberOptions, imm32);
					output.Write(s, FormatterOutputTextKind.Number);
				}
				break;

			case InstrOpKind.Immediate64:
			case InstrOpKind.Immediate8to64:
			case InstrOpKind.Immediate32to64:
				output.Write(ImmediateValuePrefix, FormatterOutputTextKind.Operator);
				if (opKind == InstrOpKind.Immediate32to64)
					imm64 = (ulong)instruction.Immediate32to64;
				else if (opKind == InstrOpKind.Immediate8to64)
					imm64 = (ulong)instruction.Immediate8to64;
				else
					imm64 = instruction.Immediate64;
				numberOptions = new NumberFormattingOptions(options, options.ShortNumbers, options.SignedImmediateOperands, false);
				if ((symbolResolver = this.symbolResolver) != null && symbolResolver.TryGetImmediateSymbol(operand, ref instruction, imm64, 8, out symbol, ref numberOptions))
					output.Write(symbol);
				else {
					if (numberOptions.SignedNumber && (long)imm64 < 0) {
						output.Write("-", FormatterOutputTextKind.Operator);
						imm64 = (ulong)-(long)imm64;
					}
					s = numberFormatter.FormatUInt64(ref numberOptions, imm64);
					output.Write(s, FormatterOutputTextKind.Number);
				}
				break;

			case InstrOpKind.MemorySegSI:
				FormatMemory(output, ref instruction, operand, instruction.MemorySize, instruction.PrefixSegment, instruction.MemorySegment, Register.SI, Register.None, 0, 0, 0, 2);
				break;

			case InstrOpKind.MemorySegESI:
				FormatMemory(output, ref instruction, operand, instruction.MemorySize, instruction.PrefixSegment, instruction.MemorySegment, Register.ESI, Register.None, 0, 0, 0, 4);
				break;

			case InstrOpKind.MemorySegRSI:
				FormatMemory(output, ref instruction, operand, instruction.MemorySize, instruction.PrefixSegment, instruction.MemorySegment, Register.RSI, Register.None, 0, 0, 0, 8);
				break;

			case InstrOpKind.MemorySegDI:
				FormatMemory(output, ref instruction, operand, instruction.MemorySize, instruction.PrefixSegment, instruction.MemorySegment, Register.DI, Register.None, 0, 0, 0, 2);
				break;

			case InstrOpKind.MemorySegEDI:
				FormatMemory(output, ref instruction, operand, instruction.MemorySize, instruction.PrefixSegment, instruction.MemorySegment, Register.EDI, Register.None, 0, 0, 0, 4);
				break;

			case InstrOpKind.MemorySegRDI:
				FormatMemory(output, ref instruction, operand, instruction.MemorySize, instruction.PrefixSegment, instruction.MemorySegment, Register.RDI, Register.None, 0, 0, 0, 8);
				break;

			case InstrOpKind.MemoryESDI:
				FormatMemory(output, ref instruction, operand, instruction.MemorySize, instruction.PrefixSegment, Register.ES, Register.DI, Register.None, 0, 0, 0, 2);
				break;

			case InstrOpKind.MemoryESEDI:
				FormatMemory(output, ref instruction, operand, instruction.MemorySize, instruction.PrefixSegment, Register.ES, Register.EDI, Register.None, 0, 0, 0, 4);
				break;

			case InstrOpKind.MemoryESRDI:
				FormatMemory(output, ref instruction, operand, instruction.MemorySize, instruction.PrefixSegment, Register.ES, Register.RDI, Register.None, 0, 0, 0, 8);
				break;

			case InstrOpKind.Memory64:
				FormatMemory(output, ref instruction, operand, instruction.MemorySize, instruction.PrefixSegment, instruction.MemorySegment, Register.None, Register.None, 0, 8, (long)instruction.MemoryAddress64, 8);
				break;

			case InstrOpKind.Memory:
				int displSize = instruction.MemoryDisplSize;
				var baseReg = instruction.MemoryBase;
				var indexReg = instruction.MemoryIndex;
				int addrSize = InstructionUtils.GetAddressSizeInBytes(baseReg, indexReg, displSize, instruction.CodeSize);
				long displ;
				if (addrSize == 8)
					displ = (int)instruction.MemoryDisplacement;
				else
					displ = instruction.MemoryDisplacement;
				if ((opInfo.Flags & InstrOpInfoFlags.IgnoreIndexReg) != 0)
					indexReg = Register.None;
				FormatMemory(output, ref instruction, operand, instruction.MemorySize, instruction.PrefixSegment, instruction.MemorySegment, baseReg, indexReg, instruction.InternalMemoryIndexScale, displSize, displ, addrSize);
				break;

			case InstrOpKind.Sae:
				FormatEvexMisc(output, "sae");
				break;

			case InstrOpKind.RnSae:
				FormatEvexMisc(output, "rn-sae");
				break;

			case InstrOpKind.RdSae:
				FormatEvexMisc(output, "rd-sae");
				break;

			case InstrOpKind.RuSae:
				FormatEvexMisc(output, "ru-sae");
				break;

			case InstrOpKind.RzSae:
				FormatEvexMisc(output, "rz-sae");
				break;

			default:
				throw new InvalidOperationException();
			}

			if (operand + 1 == opInfo.OpCount && instruction.HasOpMask) {
				output.Write("{", FormatterOutputTextKind.Punctuation);
				FormatRegister(output, instruction.OpMask);
				output.Write("}", FormatterOutputTextKind.Punctuation);
				if (instruction.ZeroingMasking)
					FormatEvexMisc(output, "z");
			}

			output.OnOperand(operand, begin: false);
		}

		void FormatEvexMisc(FormatterOutput output, string text) {
			if (options.UpperCaseOther || options.UpperCaseAll)
				text = text.ToUpperInvariant();
			output.Write("{", FormatterOutputTextKind.Punctuation);
			output.Write(text, FormatterOutputTextKind.Text);
			output.Write("}", FormatterOutputTextKind.Punctuation);
		}

		void FormatRegister(FormatterOutput output, Register reg) {
			Debug.Assert(reg != Register.None);
			Debug.Assert((uint)reg < (uint)AllRegisters.Length);
			var regStr = AllRegisters[(int)reg];
			if (options.UpperCaseRegisters || options.UpperCaseAll)
				regStr = regStr.ToUpperInvariant();
			output.Write(regStr, FormatterOutputTextKind.Register);
		}

		static readonly string[] scaleNumbers = new string[4] {
			"1", "2", "4", "8",
		};

		void FormatMemory(FormatterOutput output, ref Instruction instr, int operand, MemorySize memSize, Register segOverride, Register segReg, Register baseReg, Register indexReg, int scale, int displSize, long displ, int addrSize) {
			Debug.Assert((uint)scale < (uint)scaleNumbers.Length);
			Debug.Assert(InstructionUtils.GetAddressSizeInBytes(baseReg, indexReg, displSize, instr.CodeSize) == addrSize);

			var numberOptions = new NumberFormattingOptions(options, options.ShortNumbers, options.SignedMemoryDisplacements, options.SignExtendMemoryDisplacements);
			SymbolResult symbol;
			bool useSymbol;
			bool ripRelativeAddresses;

			ulong absAddr;
			if (baseReg == Register.RIP) {
				absAddr = (ulong)((long)instr.NextIP64 + (int)displ);
				ripRelativeAddresses = options.RipRelativeAddresses;
			}
			else if (baseReg == Register.EIP) {
				absAddr = instr.NextIP32 + (uint)displ;
				ripRelativeAddresses = options.RipRelativeAddresses;
			}
			else {
				absAddr = (ulong)displ;
				ripRelativeAddresses = true;
			}

			var symbolResolver = this.symbolResolver;
			if (symbolResolver != null)
				useSymbol = symbolResolver.TryGetDisplSymbol(operand, ref instr, absAddr, addrSize, ref ripRelativeAddresses, out symbol, ref numberOptions);
			else {
				useSymbol = false;
				symbol = default;
			}

			if (!ripRelativeAddresses) {
				if (baseReg == Register.RIP) {
					Debug.Assert(indexReg == Register.None);
					baseReg = Register.None;
					displ = (long)absAddr;
					displSize = 8;
				}
				else if (baseReg == Register.EIP) {
					Debug.Assert(indexReg == Register.None);
					baseReg = Register.None;
					displ = (long)absAddr;
					displSize = 4;
				}
			}

			bool useScale = scale != 0 || options.AlwaysShowScale;
			if (scale != 0 || options.AlwaysShowScale)
				useScale = true;
			if (addrSize == 16)
				useScale = false;

			bool hasBaseOrIndexReg = baseReg != Register.None || indexReg != Register.None;

			if (options.AlwaysShowSegmentRegister || segOverride != Register.None) {
				FormatRegister(output, segReg);
				output.Write(":", FormatterOutputTextKind.Punctuation);
			}

			if (useSymbol)
				output.Write(symbol);
			else if (!hasBaseOrIndexReg || (displSize != 0 && (options.ShowZeroDisplacements || displ != 0))) {
				if (hasBaseOrIndexReg) {
					if (addrSize == 4) {
						if (numberOptions.SignedNumber && (int)displ < 0) {
							output.Write("-", FormatterOutputTextKind.Operator);
							displ = (uint)-(int)displ;
						}
						if (numberOptions.SignExtendImmediate) {
							Debug.Assert(displSize <= 4);
							displSize = 4;
						}
					}
					else if (addrSize == 8) {
						if (numberOptions.SignedNumber && displ < 0) {
							output.Write("-", FormatterOutputTextKind.Operator);
							displ = -displ;
						}
						if (numberOptions.SignExtendImmediate) {
							Debug.Assert(displSize <= 8);
							displSize = 8;
						}
					}
					else {
						Debug.Assert(addrSize == 2);
						if (numberOptions.SignedNumber && (short)displ < 0) {
							output.Write("-", FormatterOutputTextKind.Operator);
							displ = (ushort)-(short)displ;
						}
						if (numberOptions.SignExtendImmediate) {
							Debug.Assert(displSize <= 2);
							displSize = 2;
						}
					}
				}

				string s;
				if (displSize <= 1 && (ulong)displ <= byte.MaxValue)
					s = numberFormatter.FormatUInt8(ref numberOptions, (byte)displ);
				else if (displSize <= 2 && (ulong)displ <= ushort.MaxValue)
					s = numberFormatter.FormatUInt16(ref numberOptions, (ushort)displ);
				else if (displSize <= 4 && (ulong)displ <= uint.MaxValue)
					s = numberFormatter.FormatUInt32(ref numberOptions, (uint)displ);
				else if (displSize <= 8)
					s = numberFormatter.FormatUInt64(ref numberOptions, (ulong)displ);
				else
					throw new InvalidOperationException();
				output.Write(s, FormatterOutputTextKind.Number);
			}

			if (hasBaseOrIndexReg) {
				output.Write("(", FormatterOutputTextKind.Punctuation);
				if (options.SpaceAfterMemoryOpenBracket)
					output.Write(" ", FormatterOutputTextKind.Text);

				if (baseReg != Register.None && indexReg == Register.None && !useScale)
					FormatRegister(output, baseReg);
				else {
					if (baseReg != Register.None)
						FormatRegister(output, baseReg);

					output.Write(",", FormatterOutputTextKind.Punctuation);
					if (options.SpaceAfterMemoryOperandComma)
						output.Write(" ", FormatterOutputTextKind.Text);

					if (indexReg != Register.None)
						FormatRegister(output, indexReg);

					if (useScale) {
						output.Write(",", FormatterOutputTextKind.Punctuation);
						if (options.SpaceAfterMemoryOperandComma)
							output.Write(" ", FormatterOutputTextKind.Text);

						output.Write(scaleNumbers[scale], FormatterOutputTextKind.Number);
					}
				}

				if (options.SpaceBeforeMemoryCloseBracket)
					output.Write(" ", FormatterOutputTextKind.Text);
				output.Write(")", FormatterOutputTextKind.Punctuation);
			}

			Debug.Assert((uint)memSize < (uint)allMemorySizes.Length);
			var bcstTo = allMemorySizes[(int)memSize].bcstTo;
			if (bcstTo != null)
				FormatEvexMisc(output, bcstTo);
		}

		void FormatKeyword(FormatterOutput output, string keyword) {
			if (options.UpperCaseKeywords || options.UpperCaseAll)
				keyword = keyword.ToUpperInvariant();
			output.Write(keyword, FormatterOutputTextKind.Keyword);
		}
	}
}
#endif