import importlib.util
import sys
import time
from pathlib import Path


SRC_ROOT = Path(__file__).resolve().parents[3]
CONVERSATION_STORE_PATH = SRC_ROOT / 'services' / 'Services.Python' / 'conversation_store.py'

conversation_store_spec = importlib.util.spec_from_file_location('conversation_store_test_module', CONVERSATION_STORE_PATH)
if conversation_store_spec is None or conversation_store_spec.loader is None:
    raise ImportError(f'Unable to load conversation_store from {CONVERSATION_STORE_PATH}')

conversation_store_module = importlib.util.module_from_spec(conversation_store_spec)
sys.modules[conversation_store_spec.name] = conversation_store_module
conversation_store_spec.loader.exec_module(conversation_store_module)

ConversationMessage = conversation_store_module.ConversationMessage
ConversationStoreOptions = conversation_store_module.ConversationStoreOptions
InMemoryConversationStore = conversation_store_module.InMemoryConversationStore


def test_get_or_create_conversation_without_id_creates_new_conversation():
    store = InMemoryConversationStore(ConversationStoreOptions())

    conversation = store.get_or_create_conversation()

    assert conversation.conversation_id
    assert conversation.messages == []


def test_get_or_create_conversation_with_existing_id_returns_messages():
    store = InMemoryConversationStore(ConversationStoreOptions())
    store.append_messages('conversation-1', [create_message('hello')])

    conversation = store.get_or_create_conversation('conversation-1')

    assert conversation.conversation_id == 'conversation-1'
    assert [message.content for message in conversation.messages] == ['hello']


def test_append_messages_persists_messages_in_order():
    store = InMemoryConversationStore(ConversationStoreOptions())

    store.append_messages('conversation-1', [create_message('first'), create_message('second')])

    conversation = store.get_or_create_conversation('conversation-1')
    assert [message.content for message in conversation.messages] == ['first', 'second']


def test_append_messages_trims_old_messages_when_limit_exceeded():
    store = InMemoryConversationStore(ConversationStoreOptions(maximum_messages_per_conversation=2))

    store.append_messages('conversation-1', [create_message('first'), create_message('second'), create_message('third')])

    conversation = store.get_or_create_conversation('conversation-1')
    assert [message.content for message in conversation.messages] == ['second', 'third']


def test_get_or_create_conversation_expires_old_conversations():
    store = InMemoryConversationStore(ConversationStoreOptions(expiration_timeout_seconds=0.001))
    store.append_messages('conversation-1', [create_message('old')])

    time.sleep(0.01)
    conversation = store.get_or_create_conversation('conversation-1')

    assert conversation.messages == []


def test_store_supports_multiple_simultaneous_conversations():
    store = InMemoryConversationStore(ConversationStoreOptions())

    store.append_messages('conversation-1', [create_message('first')])
    store.append_messages('conversation-2', [create_message('second')])

    assert store.get_or_create_conversation('conversation-1').messages[0].content == 'first'
    assert store.get_or_create_conversation('conversation-2').messages[0].content == 'second'


def create_message(content):
    return ConversationMessage('User', content, conversation_store_module.datetime.now(conversation_store_module.timezone.utc))