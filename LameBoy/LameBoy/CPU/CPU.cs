﻿using System;
using LameBoy.Graphics;
using System.IO;
using System.Text;
using System.Threading;
using System.Diagnostics;

namespace LameBoy
{
    public class CPU
    {
        public Registers registers = new Registers { PC = 0x100 };

        private Cart _gameCart;
        public Cart GameCart {get; set;}

        GPU gpu;
        byte instr;
        bool debugOut = false;

        public int ClockSpeed { get; private set; } = 0x400000;
        public int TotalCycles { get; private set; } = 0;

        private State _cpustate;
        public State CPUState {
            get
            {
                return _cpustate;
            }
            private set
            {
                _cpustate = value;
                StateChange(this, new EventArgs());
            }
        }

        public event EventHandler StateChange = delegate { };

        public CPU(GPU gpu)
        {
            this.gpu = gpu;
            //Thread sdlThread = new Thread(new ThreadStart(sdlt.Render));
            CPUState = State.Paused;
        }

        public void VblankInterrupt()
        {
            //gpu.SetCPUExecutionState(true);
            registers.Immediate16 = 0x0040;
            var opcode = OpcodeTable.Table[0xCD];
            opcode.Execute(ref registers, GameCart.RAM);
            //gpu.SetCPUExecutionState(false);
        }

        public void ThreadStart()
        {
            CPUState = State.Paused;
            ThreadLoop();
        }

        public void ThreadLoop()
        {
            Stopwatch sw = new Stopwatch();
            while (true)
            {
                if ((CPUState & State.Stopping) != 0)
                {
                    //Set running flag to 0
                    CPUState &= ~State.Running;
                    break;
                }

                if (CPUState != State.Running)
                {
                    Thread.Sleep(50);
                    continue;
                }

                sw.Start();
                for (int _ = 0; _ < 20000; _++)
                {
                    Execute();
                    TotalCycles++;
                }
                sw.Stop();

                double clockDiff = sw.ElapsedMilliseconds - 1000d / (ClockSpeed / 20000);
                if (clockDiff < 0)
                    Thread.Sleep((int)(-clockDiff));

                sw.Reset();
            }

            CPUState = State.Stopped;
        }

        public void Terminate()
        {
            CPUState |= State.Stopping;
        }

        public void Pause()
        {
            CPUState = State.Paused;
        }

        public void Resume()
        {
            CPUState = State.Running;
        }

        //Main interpreter loop
        public void Execute()
        {

            while (gpu.drawing) { }

            instr = GameCart.Read8(registers.PC);
            var opcode = OpcodeTable.Table[instr];
            registers.Immediate8 = GameCart.Read8(registers.PC + 1);
            registers.Immediate16 = GameCart.Read16(registers.PC + 1);

            //debug output
            if (debugOut)
            {
                StringBuilder sb = new StringBuilder();
                string disasm = opcode.Disassembly;
                if (disasm.Contains("X4"))
                    disasm = String.Format(disasm, registers.Immediate16);
                else if (disasm.Contains("X2"))
                    disasm = String.Format(disasm, registers.Immediate8);
                if(disasm.Contains("X2"))
                    disasm = String.Format(disasm, registers.A);
                sb.Clear();
                sb.Append("PC: $");
                sb.Append(registers.PC.ToString("X4"));
                sb.Append(" Disasm: ");
                sb.Append(disasm);
                sb.Append(" Opcode: ");
                sb.Append(instr.ToString("X2"));
                sb.Append("\n");
                File.AppendAllText(@"log.txt", sb.ToString());
            }

            if (opcode.Disassembly.Contains("UNIMP"))
            {
                string disasm = opcode.Disassembly;
                if (disasm.Contains("X4"))
                    disasm = String.Format(disasm, registers.Immediate16);
                else if (disasm.Contains("X2"))
                    disasm = String.Format(disasm, registers.Immediate8);
                //debugOut = true;
                Console.WriteLine("Unimplemented opcode: {0:X2}", instr);
                Console.WriteLine("PC: ${0:X4} Disasm: {1} Opcode: {2:X2}", registers.PC, disasm, instr);
                //Console.ReadLine();
            }

            registers.PC += opcode.Length;
            registers.M = opcode.M;
            registers.T = opcode.T;

            opcode.Execute(ref registers, GameCart.RAM);

            registers.TotalM += registers.M;
            registers.TotalT += registers.T;

            //gpu.SetCPUExecutionState(false);
            if (gpu.GetYCounter() == 154 && registers.Interrupts)
            {
                VblankInterrupt();
            }
        }
    }
}
