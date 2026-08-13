# Cloud Agent base image for VilmoMarketingEngine.
# Pins the .NET 10 LTS SDK so the toolchain is stable and available on PATH.
FROM mcr.microsoft.com/dotnet/sdk:10.0

# System tools used by Cloud Agents and everyday development.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        git \
        curl \
        ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Deterministic, quiet CLI output.
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

# Run as a non-root user that Cloud Agents expect.
ARG USERNAME=ubuntu
RUN if ! id -u "${USERNAME}" >/dev/null 2>&1; then \
        useradd --create-home --shell /bin/bash "${USERNAME}"; \
    fi

USER ${USERNAME}
WORKDIR /home/${USERNAME}
