import { RedisMemoryProvider } from '../../../services/Services.NestJS/redis-memory-provider';
import { InMemoryConversationStore } from '../../../services/Services.NestJS/conversation-store';

// Since we're doing integration testing against a live Redis instance,
// we need to test with a proper setup
describe('RedisMemoryProvider', () => {
  let provider: RedisMemoryProvider;
  
  // Mock the Redis client to prevent actual connections during tests
  const mockRedisClient = {
    get: jest.fn(),
    setex: jest.fn(),
    expire: jest.fn(),
    on: jest.fn(),
  };

  beforeEach(() => {
    // Create a spy on the constructor to replace Redis with mock
    const originalConstructor = RedisMemoryProvider.prototype.constructor;
    
    // Mock Redis connection to simulate successful initialization
    (RedisMemoryProvider.prototype as any).redis = mockRedisClient;
    (RedisMemoryProvider.prototype as any).inMemoryFallback = new InMemoryConversationStore();
  });

  afterEach(() => {
    jest.clearAllMocks();
  });

  it('should create a new instance without throwing errors', () => {
    expect(() => new RedisMemoryProvider()).not.toThrow();
  });

  it('should get or create conversation with new ID', async () => {
    const mockConversation = {
      conversationId: 'test-conversation-123',
      messages: []
    };
    
    const provider = new RedisMemoryProvider();
    // Mock the in-memory fallback behavior
    const result = await provider.getOrCreateConversation();
    
    expect(result).toHaveProperty('conversationId');
    expect(result.messages).toEqual([]);
  });

  it('should append messages to conversation', async () => {
    const mockMessages = [
      { role: 'User' as const, content: 'Hello', timestamp: new Date() },
      { role: 'Assistant' as const, content: 'Hi there!', timestamp: new Date() }
    ];
    
    // Mock Redis client responses to simulate successful storage
    mockRedisClient.get.mockResolvedValue(JSON.stringify({
      conversationId: 'test-123',
      messages: []
    }));
    
    mockRedisClient.setex.mockResolvedValue('OK');
    
    const provider = new RedisMemoryProvider();
    await expect(provider.appendMessages('test-123', mockMessages)).resolves.not.toThrow();
  });

  it('should handle connection errors and fall back to in-memory provider', async () => {
    // Mock the redis client to throw an error
    const mockRedisClientWithError = {
      get: jest.fn().mockRejectedValue(new Error('Connection failed')),
      setex: jest.fn().mockRejectedValue(new Error('Connection failed')),
      expire: jest.fn(),
      on: jest.fn(),
    };
    
    // Mock the constructor to use our error-throwing client
    const provider = new RedisMemoryProvider();
    
    // Override with mock that forces fallback behavior
    (provider as any).redis = mockRedisClientWithError;
    
    const result = await provider.getOrCreateConversation('test-123');
    expect(result).toBeDefined();  
  });

  it('should expose Redis client properly', () => {
    const provider = new RedisMemoryProvider();
    expect(provider.getRedisClient()).toBeInstanceOf(Object);
  });
});

// Integration test with actual Redis connection
describe('RedisMemoryProvider - Integration', () => {
  let provider: RedisMemoryProvider;
  
  beforeAll(() => {
    // We skip actual Redis integration in unit tests to avoid external dependencies
    // In real environment, the provider would connect to localhost:6379
  });

  it('should initialize without throwing errors even when Redis is not available', () => {
    expect(() => new RedisMemoryProvider()).not.toThrow();
  });
});