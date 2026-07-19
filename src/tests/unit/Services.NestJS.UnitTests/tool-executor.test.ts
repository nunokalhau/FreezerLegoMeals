import { mkdtempSync, rmSync, writeFileSync } from 'fs';
import { join } from 'path';
import { tmpdir } from 'os';
import { ToolExecutor, ToolHandler } from '../../../services/Services.NestJS/tool-executor';
import { ToolRegistry } from '../../../services/Services.NestJS/tool-registry';

describe('NestJS ToolExecutor architecture', () => {
  let tmpRoot: string;
  let registryPath: string;

  beforeEach(() => {
    tmpRoot = mkdtempSync(join(tmpdir(), 'tool-registry-'));
    registryPath = join(tmpRoot, 'tool_registry.json');
    writeFileSync(
      registryPath,
      JSON.stringify({
        tools: [
          {
            name: 'example_tool',
            description: 'Example tool',
            script: 'example_tool.py',
            aliases: ['example_alias'],
          },
        ],
      })
    );
  });

  afterEach(() => {
    rmSync(tmpRoot, { recursive: true, force: true });
  });

  it('loads tools from the shared registry shape and resolves aliases', () => {
    const registry = new ToolRegistry(registryPath);

    expect(registry.getTools()).toHaveLength(1);
    expect(registry.findTool('example_alias').name).toBe('example_tool');
  });

  it('delegates execution to a registered application service handler', async () => {
    const handler: ToolHandler = {
      toolName: 'example_tool',
      execute: jest.fn().mockResolvedValue({ handled: true }),
    };
    const executor = new ToolExecutor(new ToolRegistry(registryPath), [handler]);

    const result = await executor.execute('example_alias', { message: 'hello' });

    expect(result).toEqual({
      success: true,
      tool: 'example_tool',
      output: { handled: true },
    });
    expect(handler.execute).toHaveBeenCalledWith({ message: 'hello' });
  });

  it('does not fall back to CLI scripts when no handler exists', async () => {
    const executor = new ToolExecutor(new ToolRegistry(registryPath));

    const result = await executor.execute('example_tool');

    expect(result.success).toBe(false);
    expect(result.tool).toBe('example_tool');
    expect(result.error).toContain('No application service handler');
  });

  it('throws for unknown tools', async () => {
    const executor = new ToolExecutor(new ToolRegistry(registryPath));

    await expect(executor.execute('missing_tool')).rejects.toThrow('Unknown tool: missing_tool');
  });
});