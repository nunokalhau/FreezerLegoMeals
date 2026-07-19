import { mkdtempSync, rmSync, writeFileSync } from 'fs';
import { join } from 'path';
import { tmpdir } from 'os';
import { ToolExecutor } from '../../../services/Services.NestJS/tool-executor';
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
            wrapper: 'example_tool.py',
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
    writeFileSync(
      join(tmpRoot, 'example_tool.py'),
      "import json, sys\nparameters = json.loads(sys.stdin.read() or '{}')\nprint(json.dumps({'handled': True, 'parameters': parameters}))\n"
    );
    const executor = new ToolExecutor(new ToolRegistry(registryPath), tmpRoot);

    const result = await executor.execute('example_alias', { message: 'hello' });

    expect(result).toEqual({ success: true, tool: 'example_tool', output: { handled: true, parameters: { message: 'hello' } } });
  });

  it('returns failure when the wrapper is missing', async () => {
    const executor = new ToolExecutor(new ToolRegistry(registryPath), tmpRoot);

    const result = await executor.execute('example_tool');

    expect(result.success).toBe(false);
    expect(result.tool).toBe('example_tool');
    expect(result.error).toContain('Tool wrapper not found');
  });

  it('throws for unknown tools', async () => {
    const executor = new ToolExecutor(new ToolRegistry(registryPath), tmpRoot);

    await expect(executor.execute('missing_tool')).rejects.toThrow('Unknown tool: missing_tool');
  });
});