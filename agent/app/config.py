from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    # Model (never hardcode secrets; keys only via env)
    llm_provider: str = "mock"  # mock | openai
    llm_model: str = "gpt-4o-mini"
    openai_api_key: str | None = None

    # Vilmo API integration
    vilmo_api_base_url: str = "http://localhost:5080"
    vilmo_api_token: str | None = None
    vilmo_company_id: str | None = None
    tool_timeout_seconds: float = 5.0
    tool_max_retries: int = 2

    # Autonomy
    allow_destructive_actions: bool = False
    require_human_approval_for_publish: bool = True
    max_graph_steps: int = 12

    # Memory / RAG
    knowledge_dir: str = "agent/data/knowledge"
    memory_dir: str = "agent/data/memory"
    rag_top_k: int = 3

    # Observability
    service_name: str = "vilmo-sctec-agent"
    audit_log_path: str = "agent/data/audit.jsonl"

    # Low-code webhook target (optional ChatOps)
    alert_webhook_url: str | None = None


@lru_cache
def get_settings() -> Settings:
    return Settings()
