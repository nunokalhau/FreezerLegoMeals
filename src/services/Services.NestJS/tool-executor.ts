import { ToolDefinition, ToolRegistry } from './tool-registry';
import { spawn } from 'child_process';
import { existsSync } from 'fs';
import { resolve } from 'path';

export type ToolParameters = Record<string, unknown>;

export interface ToolExecutionResult {
  success: boolean;
  tool: string;
  output?: unknown;
  error?: string;
}

export interface ToolHandler {
  toolName: string;
  execute(parameters: ToolParameters): Promise<unknown>;
}

export class ToolExecutor {
  // TODO: Add Redis-backed execution metadata/history and reusable result caching when tool execution needs cross-instance observability.
  // TODO: Add RedisToolExecutor, MCPToolExecutor, DockerToolExecutor, and RemoteToolExecutor implementations behind this executor contract.
  constructor(
    private readonly toolRegistry: ToolRegistry,
    private readonly toolsRoot: string,
    private readonly pythonExecutable = 'python'
  ) {}

  getTools(): ToolDefinition[] {
    return this.toolRegistry.getTools();
  }

  async execute(toolName: string, parameters: ToolParameters = {}): Promise<ToolExecutionResult> {
    const tool = this.toolRegistry.findTool(toolName);

    try {
      const wrapper = this.resolveWrapper(tool);
      const output = await this.executeWrapper(wrapper, parameters);
      return { success: true, tool: tool.name, output };
    } catch (error) {
      return { success: false, tool: tool.name, error: error instanceof Error ? error.message : String(error) };
    }
  }

  private resolveWrapper(tool: ToolDefinition): string {
    const wrapper = tool.wrapper ?? tool.script;
    if (!wrapper) {
      throw new Error(`Tool '${tool.name}' does not define a wrapper.`);
    }

    const wrapperPath = resolve(this.toolsRoot, wrapper);
    if (!existsSync(wrapperPath)) {
      throw new Error(`Tool wrapper not found for '${tool.name}': ${wrapperPath}`);
    }

    return wrapperPath;
  }

  private executeWrapper(wrapper: string, parameters: ToolParameters): Promise<unknown> {
    return new Promise((resolveOutput, reject) => {
      const process = spawn(this.pythonExecutable, [wrapper], { cwd: this.toolsRoot, windowsHide: true });
      let stdout = '';
      let stderr = '';

      process.stdout.on('data', (chunk) => { stdout += chunk.toString(); });
      process.stderr.on('data', (chunk) => { stderr += chunk.toString(); });
      process.on('error', reject);
      process.on('close', (code) => {
        if (code !== 0) {
          reject(new Error(stderr.trim() || `Tool wrapper exited with code ${code}`));
          return;
        }

        try {
          resolveOutput(JSON.parse(stdout));
        } catch (error) {
          reject(new Error(`Tool wrapper returned invalid JSON: ${error instanceof Error ? error.message : String(error)}`));
        }
      });

      process.stdin.write(JSON.stringify(parameters));
      process.stdin.end();
    });
  }
}