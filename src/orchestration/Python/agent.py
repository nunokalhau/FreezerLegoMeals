from __future__ import annotations

from abc import ABC, abstractmethod


class Agent(ABC):
    @property
    @abstractmethod
    def name(self) -> str:
        raise NotImplementedError

    @abstractmethod
    def can_handle(self, context) -> bool:
        raise NotImplementedError

    @abstractmethod
    def execute(self, context):
        raise NotImplementedError