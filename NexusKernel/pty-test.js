import os from 'os';
import pty from 'node-pty';

const shell = os.platform() === 'win32' ? 'cmd.exe' : 'bash';

const ptyProcess = pty.spawn(shell, ['/c', 'gemini'], {
  name: 'xterm-color',
  cols: 80,
  rows: 30,
  cwd: process.cwd(),
  env: process.env
});

ptyProcess.onData((data) => {
  console.log("PTY OUTPUT:", JSON.stringify(data));
  if (data.includes("How can I help you today?")) {
      console.log("Sending prompt...");
      ptyProcess.write("hello\r");
  }
});
