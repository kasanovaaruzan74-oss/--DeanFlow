from contextlib import asynccontextmanager

from fastapi import FastAPI
from sqlalchemy import Engine
from sqlmodel import SQLModel

from app.database.db import engine
from app.routers.documents import router as documents_router


def create_app(database_engine: Engine = engine) -> FastAPI:
    @asynccontextmanager
    async def lifespan(_app: FastAPI):
        SQLModel.metadata.create_all(database_engine)
        yield

    application = FastAPI(
        title="DeanFlow оқу API",
        description="Деканат құжаттарын басқарудың зертханалық REST API үлгісі.",
        version="1.0.0",
        lifespan=lifespan,
    )
    application.include_router(documents_router)

    @application.get("/", tags=["Жүйе"])
    def root() -> dict[str, str]:
        return {"жүйе": "DeanFlow оқу API", "құжаттама": "/docs"}

    @application.get("/health", tags=["Жүйе"])
    def health() -> dict[str, str]:
        return {"күйі": "жұмыс істеп тұр"}

    return application


app = create_app()
