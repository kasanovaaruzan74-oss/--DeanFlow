from datetime import date, datetime, timezone
from typing import Literal

from pydantic import ConfigDict
from sqlmodel import Field, Relationship, SQLModel


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


class Role(SQLModel, table=True):
    __tablename__ = "roles"

    role_id: int | None = Field(default=None, primary_key=True)
    role_name: str = Field(index=True, unique=True, max_length=50)
    description: str | None = Field(default=None, max_length=255)

    users: list["AppUser"] = Relationship(back_populates="role")


class Department(SQLModel, table=True):
    __tablename__ = "departments"

    department_id: int | None = Field(default=None, primary_key=True)
    department_name: str = Field(index=True, unique=True, max_length=150)
    short_name: str | None = Field(default=None, max_length=30)
    is_active: bool = True

    users: list["AppUser"] = Relationship(back_populates="department")
    documents: list["DeanDocument"] = Relationship(back_populates="department")


class AppUser(SQLModel, table=True):
    __tablename__ = "users"

    user_id: int | None = Field(default=None, primary_key=True)
    role_id: int = Field(foreign_key="roles.role_id", index=True)
    department_id: int | None = Field(default=None, foreign_key="departments.department_id", index=True)
    full_name: str = Field(index=True, max_length=160)
    email: str = Field(index=True, unique=True, max_length=254)
    is_active: bool = True
    created_at: datetime = Field(default_factory=utc_now)

    role: Role = Relationship(back_populates="users")
    department: Department | None = Relationship(back_populates="users")
    created_documents: list["DeanDocument"] = Relationship(back_populates="created_by")
    assignments: list["ExecutorAssignment"] = Relationship(
        back_populates="user",
        sa_relationship_kwargs={"foreign_keys": "ExecutorAssignment.user_id"},
    )
    comments: list["DocumentComment"] = Relationship(back_populates="user")


class DocumentType(SQLModel, table=True):
    __tablename__ = "document_types"

    document_type_id: int | None = Field(default=None, primary_key=True)
    type_name: str = Field(index=True, unique=True, max_length=100)
    code: str = Field(index=True, unique=True, max_length=20)
    is_active: bool = True

    documents: list["DeanDocument"] = Relationship(back_populates="document_type")


class DocumentStatus(SQLModel, table=True):
    __tablename__ = "document_statuses"

    status_id: int | None = Field(default=None, primary_key=True)
    status_name: str = Field(index=True, max_length=70)
    status_code: str = Field(index=True, unique=True, max_length=30)

    documents: list["DeanDocument"] = Relationship(back_populates="status")


class DeanDocument(SQLModel, table=True):
    __tablename__ = "documents"

    document_id: int | None = Field(default=None, primary_key=True)
    registration_number: str = Field(index=True, unique=True, max_length=50)
    document_type_id: int = Field(foreign_key="document_types.document_type_id", index=True)
    status_id: int = Field(foreign_key="document_statuses.status_id", index=True)
    department_id: int | None = Field(default=None, foreign_key="departments.department_id", index=True)
    created_by_user_id: int = Field(foreign_key="users.user_id", index=True)
    title: str = Field(index=True, max_length=250)
    description: str | None = Field(default=None, max_length=3000)
    direction: str = Field(default="Ішкі", max_length=20)
    priority: str = Field(default="Қалыпты", max_length=20)
    registered_at: datetime = Field(default_factory=utc_now, index=True)
    due_date: date | None = None
    completed_at: datetime | None = None
    is_archived: bool = False

    document_type: DocumentType = Relationship(back_populates="documents")
    status: DocumentStatus = Relationship(back_populates="documents")
    department: Department | None = Relationship(back_populates="documents")
    created_by: AppUser = Relationship(back_populates="created_documents")
    executors: list["ExecutorAssignment"] = Relationship(back_populates="document")
    approvals: list["DocumentApproval"] = Relationship(back_populates="document")
    history: list["DocumentHistoryEntry"] = Relationship(back_populates="document")
    comments: list["DocumentComment"] = Relationship(back_populates="document")
    attachments: list["DocumentAttachment"] = Relationship(back_populates="document")


class ExecutorAssignment(SQLModel, table=True):
    __tablename__ = "document_executors"

    assignment_id: int | None = Field(default=None, primary_key=True)
    document_id: int = Field(foreign_key="documents.document_id", index=True, ondelete="CASCADE")
    user_id: int = Field(foreign_key="users.user_id", index=True)
    assigned_by_user_id: int = Field(foreign_key="users.user_id")
    task_description: str | None = Field(default=None, max_length=500)
    due_date: date | None = None
    completed_at: datetime | None = None

    document: DeanDocument = Relationship(back_populates="executors")
    user: AppUser = Relationship(back_populates="assignments", sa_relationship_kwargs={"foreign_keys": "ExecutorAssignment.user_id"})


class DocumentApproval(SQLModel, table=True):
    __tablename__ = "document_approvals"

    approval_id: int | None = Field(default=None, primary_key=True)
    document_id: int = Field(foreign_key="documents.document_id", index=True, ondelete="CASCADE")
    approver_user_id: int = Field(foreign_key="users.user_id", index=True)
    approval_order: int = Field(default=1, ge=1)
    approval_status: str = Field(default="Күтуде", max_length=30)
    comment: str | None = Field(default=None, max_length=1000)
    assigned_at: datetime = Field(default_factory=utc_now)
    decision_at: datetime | None = None

    document: DeanDocument = Relationship(back_populates="approvals")


class DocumentHistoryEntry(SQLModel, table=True):
    __tablename__ = "document_history"

    history_id: int | None = Field(default=None, primary_key=True)
    document_id: int = Field(foreign_key="documents.document_id", index=True, ondelete="CASCADE")
    user_id: int | None = Field(default=None, foreign_key="users.user_id")
    action_type: str = Field(max_length=100)
    description: str | None = Field(default=None, max_length=1000)
    action_date: datetime = Field(default_factory=utc_now, index=True)

    document: DeanDocument = Relationship(back_populates="history")


class DocumentComment(SQLModel, table=True):
    __tablename__ = "document_comments"

    comment_id: int | None = Field(default=None, primary_key=True)
    document_id: int = Field(foreign_key="documents.document_id", index=True, ondelete="CASCADE")
    user_id: int = Field(foreign_key="users.user_id", index=True)
    comment_text: str = Field(max_length=1500)
    created_at: datetime = Field(default_factory=utc_now)

    document: DeanDocument = Relationship(back_populates="comments")
    user: AppUser = Relationship(back_populates="comments")


class DocumentAttachment(SQLModel, table=True):
    __tablename__ = "attachments"

    attachment_id: int | None = Field(default=None, primary_key=True)
    document_id: int = Field(foreign_key="documents.document_id", index=True, ondelete="CASCADE")
    uploaded_by_user_id: int = Field(foreign_key="users.user_id", index=True)
    file_name: str = Field(max_length=255)
    file_type: str | None = Field(default=None, max_length=100)
    file_size: int | None = Field(default=None, ge=0)
    uploaded_at: datetime = Field(default_factory=utc_now)

    document: DeanDocument = Relationship(back_populates="attachments")


class DocumentCreate(SQLModel):
    model_config = ConfigDict(str_strip_whitespace=True)

    title: str = Field(min_length=3, max_length=250)
    description: str | None = Field(default=None, max_length=3000)
    document_type_id: int = Field(gt=0)
    status_id: int = Field(default=1, gt=0)
    department_id: int | None = Field(default=None, gt=0)
    created_by_user_id: int = Field(default=1, gt=0)
    direction: Literal["Кіріс", "Шығыс", "Ішкі"] = "Ішкі"
    priority: Literal["Төмен", "Қалыпты", "Жоғары", "Шұғыл"] = "Қалыпты"
    due_date: date | None = None


class DocumentUpdate(SQLModel):
    model_config = ConfigDict(str_strip_whitespace=True)

    title: str | None = Field(default=None, min_length=3, max_length=250)
    description: str | None = Field(default=None, max_length=3000)
    document_type_id: int | None = Field(default=None, gt=0)
    status_id: int | None = Field(default=None, gt=0)
    department_id: int | None = Field(default=None, gt=0)
    direction: Literal["Кіріс", "Шығыс", "Ішкі"] | None = None
    priority: Literal["Төмен", "Қалыпты", "Жоғары", "Шұғыл"] | None = None
    due_date: date | None = None


class DocumentRead(SQLModel):
    document_id: int
    registration_number: str
    document_type_id: int
    status_id: int
    department_id: int | None
    created_by_user_id: int
    title: str
    description: str | None
    direction: str
    priority: str
    registered_at: datetime
    due_date: date | None
    completed_at: datetime | None
    is_archived: bool


class LookupItem(SQLModel):
    id: int
    name: str
    code: str | None = None
