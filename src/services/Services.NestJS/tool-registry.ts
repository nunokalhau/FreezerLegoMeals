import { readFileSync } from 'fs';

export interface ToolDefinition {
  name: string;
  description: string;
  script?: string;
  wrapper?: string;
  keywords?: string[];
  parameters?: string[];
  examples?: string[];
  result_example?: unknown;
  expected_json_output?: unknown;
  output_description?: string;
  aliases?: string[];
}

interface ToolRegistryDocument {
  tools?: ToolDefinition[];
}

export class ToolRegistry {
  private readonly tools: ToolDefinition[];

  constructor(registryPath: string) {
    const registry = JSON.parse(readFileSync(registryPath, 'utf8')) as ToolRegistryDocument;

    if (!Array.isArray(registry.tools)) {
      throw new Error('Tool registry must contain a tools array');
    }

    this.tools = registry.tools;
  }

  getTools(): ToolDefinition[] {
    return this.tools.map((tool) => ({ ...tool }));
  }

  findTool(toolName: string): ToolDefinition {
    const tool = this.tools.find(
      (item) => item.name === toolName || (item.aliases ?? []).includes(toolName)
    );

    if (!tool) {
      throw new Error(`Unknown tool: ${toolName}`);
    }

    return { ...tool };
  }
}