from collections.abc import Generator

import pytest
from fastapi.testclient import TestClient
from sqlmodel import Session

from app.database.db import create_database_engine, get_session
from app.database.models import AppUser, Department, DocumentStatus, DocumentType, Role
from app.main import create_app


@pytest.fixture
def client(tmp_path) -> Generator[TestClient, None, None]:
    database_engine = create_database_engine(f"sqlite:///{tmp_path / 'test.db'}")
    application = create_app(database_engine)
    application.state.database_engine = database_engine

    def get_test_session():
        with Session(database_engine) as session:
            yield session

    application.dependency_overrides[get_session] = get_test_session
    with TestClient(application) as test_client:
        with Session(database_engine) as session:
            role = Role(role_name="DeanerySpecialist")
            department = Department(department_name="Оқу бөлімі", short_name="ОБ")
            document_type = DocumentType(type_name="Өтініш", code="REQUEST")
            status = DocumentStatus(status_name="Тіркелді", status_code="REGISTERED")
            session.add_all([role, department, document_type, status])
            session.commit()
            session.refresh(role)
            session.refresh(department)
            session.add(
                AppUser(
                    role_id=role.role_id,
                    department_id=department.department_id,
                    full_name="Айгерім Тестова",
                    email="test@example.invalid",
                )
            )
            session.commit()
        yield test_client
    database_engine.dispose()
