from datetime import date
from uuid import uuid4

from fastapi import APIRouter, Depends, HTTPException, Query, Response, status
from sqlalchemy import or_
from sqlmodel import Session, desc, select

from app.database.db import get_session
from app.database.models import (
    AppUser,
    Department,
    DeanDocument,
    DocumentApproval,
    DocumentAttachment,
    DocumentComment,
    DocumentCreate,
    DocumentHistoryEntry,
    DocumentRead,
    DocumentStatus,
    DocumentType,
    DocumentUpdate,
    ExecutorAssignment,
    LookupItem,
)


router = APIRouter(prefix="/documents", tags=["Құжаттар"])


@router.get("/lookups")
def get_lookups(session: Session = Depends(get_session)) -> dict[str, list[LookupItem]]:
    types = session.exec(select(DocumentType).where(DocumentType.is_active)).all()
    statuses = session.exec(select(DocumentStatus)).all()
    departments = session.exec(select(Department).where(Department.is_active)).all()
    return {
        "types": [LookupItem(id=item.document_type_id, name=item.type_name, code=item.code) for item in types],
        "statuses": [LookupItem(id=item.status_id, name=item.status_name, code=item.status_code) for item in statuses],
        "departments": [LookupItem(id=item.department_id, name=item.department_name) for item in departments],
    }


@router.get("", response_model=list[DocumentRead])
def list_documents(
    search: str | None = Query(default=None, max_length=120),
    status_code: str | None = Query(default=None, max_length=30),
    direction: str | None = Query(default=None, max_length=20),
    include_archived: bool = False,
    limit: int = Query(default=100, ge=1, le=200),
    session: Session = Depends(get_session),
) -> list[DeanDocument]:
    statement = select(DeanDocument)
    if not include_archived:
        statement = statement.where(DeanDocument.is_archived.is_(False))
    if search and search.strip():
        pattern = f"%{search.strip()}%"
        statement = statement.where(
            or_(DeanDocument.title.ilike(pattern), DeanDocument.registration_number.ilike(pattern))
        )
    if status_code:
        statement = statement.join(DocumentStatus).where(DocumentStatus.status_code == status_code)
    if direction:
        statement = statement.where(DeanDocument.direction == direction)
    return list(session.exec(statement.order_by(desc(DeanDocument.registered_at)).limit(limit)).all())


@router.get("/{document_id}", response_model=DocumentRead)
def get_document(document_id: int, session: Session = Depends(get_session)) -> DeanDocument:
    document = session.get(DeanDocument, document_id)
    if document is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Құжат табылмады")
    return document


@router.post("", response_model=DocumentRead, status_code=status.HTTP_201_CREATED)
def create_document(payload: DocumentCreate, session: Session = Depends(get_session)) -> DeanDocument:
    _validate_references(session, payload.document_type_id, payload.status_id, payload.department_id, payload.created_by_user_id)
    document = DeanDocument(
        registration_number=f"DF-{date.today().year}-{uuid4().hex[:8].upper()}",
        **payload.model_dump(),
    )
    session.add(document)
    session.commit()
    session.refresh(document)
    return document


@router.patch("/{document_id}", response_model=DocumentRead)
def update_document(
    document_id: int,
    payload: DocumentUpdate,
    session: Session = Depends(get_session),
) -> DeanDocument:
    document = session.get(DeanDocument, document_id)
    if document is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Құжат табылмады")

    changes = payload.model_dump(exclude_unset=True)
    if not changes:
        raise HTTPException(status_code=status.HTTP_422_UNPROCESSABLE_ENTITY, detail="Өзгертілетін өрісті көрсетіңіз")
    for required_field in ("title", "document_type_id", "status_id", "direction", "priority"):
        if required_field in changes and changes[required_field] is None:
            raise HTTPException(status_code=status.HTTP_422_UNPROCESSABLE_ENTITY, detail="Міндетті өрісті бос қалдыруға болмайды")
    _validate_references(
        session,
        changes.get("document_type_id", document.document_type_id),
        changes.get("status_id", document.status_id),
        changes.get("department_id", document.department_id),
    )

    document.sqlmodel_update(changes)
    session.add(document)
    session.commit()
    session.refresh(document)
    return document


@router.delete("/{document_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_document(document_id: int, session: Session = Depends(get_session)) -> Response:
    document = session.get(DeanDocument, document_id)
    if document is None:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail="Құжат табылмады")

    # Оған байланған жазбалар FK арқылы бірге өшеді; қолданушы мен анықтамалық кестелер сақталады.
    for child_model in (ExecutorAssignment, DocumentApproval, DocumentHistoryEntry, DocumentComment, DocumentAttachment):
        children = session.exec(select(child_model).where(child_model.document_id == document_id)).all()
        for child in children:
            session.delete(child)
    session.delete(document)
    session.commit()
    return Response(status_code=status.HTTP_204_NO_CONTENT)


def _validate_references(
    session: Session,
    document_type_id: int,
    status_id: int,
    department_id: int | None,
    created_by_user_id: int | None = None,
) -> None:
    if session.get(DocumentType, document_type_id) is None:
        raise HTTPException(status_code=422, detail="Құжат түрі табылмады")
    if session.get(DocumentStatus, status_id) is None:
        raise HTTPException(status_code=422, detail="Құжат мәртебесі табылмады")
    if department_id is not None and session.get(Department, department_id) is None:
        raise HTTPException(status_code=422, detail="Бөлім табылмады")
    if created_by_user_id is not None and session.get(AppUser, created_by_user_id) is None:
        raise HTTPException(status_code=422, detail="Пайдаланушы табылмады")
