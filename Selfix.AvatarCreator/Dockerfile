# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# Copy project files first to leverage Docker cache for restore
COPY ["Selfix.AvatarCreator.EntryPoint/Selfix.AvatarCreator.EntryPoint.csproj", "Selfix.AvatarCreator.EntryPoint/"]
COPY ["Selfix.AvatarCreator.Application/Selfix.AvatarCreator.Application.csproj", "Selfix.AvatarCreator.Application/"]
COPY ["Selfix.AvatarCreator.Infrastructure/Selfix.AvatarCreator.Infrastructure.csproj", "Selfix.AvatarCreator.Infrastructure/"]
COPY ["Selfix.AvatarCreator.Shared/Selfix.AvatarCreator.Shared.csproj", "Selfix.AvatarCreator.Shared/"]
COPY ["Submodules/Selfix.Schema/Selfix.Schema/Selfix.Schema.csproj", "Submodules/Selfix.Schema/Selfix.Schema/"]
RUN dotnet restore "Selfix.AvatarCreator.EntryPoint/Selfix.AvatarCreator.EntryPoint.csproj"
# Copy the rest of the code
COPY . .
WORKDIR "/src/Selfix.AvatarCreator.EntryPoint"
RUN dotnet publish "Selfix.AvatarCreator.EntryPoint.csproj" -c Release -o /app/publish

# Runtime stage
FROM nvidia/cuda:12.4.1-base-ubuntu22.04 AS runtime

# Install .NET Runtime and Python dependencies
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    wget \
    apt-transport-https \
    software-properties-common \
    libgl1 \
    libglib2.0-0 \
    libsm6 \
    libxext6 \
    libxrender-dev \
    && \
    wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
    dotnet-runtime-9.0 \
    python3-pip \
    python3-dev \
    git \
    build-essential && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* 

# Install opencv
# RUN pip install --no-cache-dir opencv-python-headless

# Create non-root user
ENV PUID=${PUID:-1000}
ENV PGID=${PGID:-1000}
RUN groupadd -g "${PGID}" appuser && \
    useradd -m -s /bin/sh -u "${PUID}" -g "${PGID}" appuser

RUN mkdir -p /workspace
WORKDIR /workspace

# Setup ML environment
RUN git clone -b sd3 https://github.com/kohya-ss/sd-scripts && \
    cd sd-scripts && \
    git checkout 5a18a03ffcc2a21c6e884a25d041076911a79a2a && \
    pip install --no-cache-dir -r ./requirements.txt
RUN pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu124

COPY ["requirements.txt", "requirements.txt"]
RUN pip install --no-cache-dir -r ./requirements.txt

# Create workspace directories
RUN mkdir -p /workspace/models/unet /workspace/models/clip /workspace/models/vae /workspace/models/captioning/florence2l /workspace/input /workspace/output
COPY ["Scripts/generate-captions.py", "input/"]
COPY ["Templates/dataset-template-lowvram.toml", "input/dataset-lowvram.toml"]
COPY ["Templates/dataset-template-highvram.toml", "input/dataset-highvram.toml"]

# Copy the built app
WORKDIR /app
COPY --from=build /app/publish .

# Set ownership
RUN chown -R appuser:appuser /app
RUN chown -R appuser:appuser /workspace

# Use non-root user
USER appuser

ENTRYPOINT ["dotnet", "Selfix.AvatarCreator.EntryPoint.dll"]