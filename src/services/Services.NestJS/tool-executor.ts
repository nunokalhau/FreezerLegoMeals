import { ToolDefinition, ToolRegistry } from './tool-registry';

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
  private readonly handlers: Map<string, ToolHandler>;

  constructor(
    private readonly toolRegistry: ToolRegistry,
    handlers: ToolHandler[] = []
  ) {
    this.handlers = new Map(handlers.map((handler) => [handler.toolName, handler]));
  }

  getTools(): ToolDefinition[] {
    return this.toolRegistry.getTools();
  }

  async execute(toolName: string, parameters: ToolParameters = {}): Promise<ToolExecutionResult> {
    const tool = this.toolRegistry.findTool(toolName);
    const handler = this.handlers.get(tool.name);

    if (!handler) {
      return {
        success: false,
        tool: tool.name,
        error: `No application service handler registered for tool: ${tool.name}`,
      };
    }

    return {
      success: true,
      tool: tool.name,
      output: await handler.execute(parameters),
    };
  }
}