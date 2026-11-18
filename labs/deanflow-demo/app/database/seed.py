from datetime import date, datetime, timedelta, timezone

from sqlmodel import Session, select

from app.database.db import engine, init_db
from app.database.models import (
    AppUser,
    Department,
    DeanDocument,
    DocumentApproval,
    DocumentAttachment,
    DocumentComment,
    DocumentHistoryEntry,
    DocumentStatus,
    DocumentType,
    ExecutorAssignment,
    Role,
)


def seed_database() -> None:
    init_db(engine)
    with Session(engine) as session:
        for name, description in [
            ("Administrator", "Жүйені әкімшілендіру"),
            ("DeanerySpecialist", "Құжаттарды тіркеу және өңдеу"),
            ("Executor", "Тапсырмаларды орындау"),
        ]:
            if session.exec(select(Role).where(Role.role_name == name)).first() is None:
                session.add(Role(role_name=name, description=description))
        for name, short in [("Оқу бөлімі", "ОБ"), ("Тіркеу бөлімі", "ТБ"), ("Әкімшілік бөлім", "ӘБ")]:
            if session.exec(select(Department).where(Department.department_name == name)).first() is None:
                session.add(Department(department_name=name, short_name=short))
        for name, code in [("Студент өтініші", "REQUEST"), ("Қызметтік хат", "MEMO"), ("Бұйрық жобасы", "ORDER")]:
            if session.exec(select(DocumentType).where(DocumentType.code == code)).first() is None:
                session.add(DocumentType(type_name=name, code=code))
        for name, code in [
            ("Тіркелді", "REGISTERED"),
            ("Орындалуда", "IN_PROGRESS"),
            ("Келісуде", "APPROVAL"),
            ("Аяқталды", "COMPLETED"),
        ]:
            if session.exec(select(DocumentStatus).where(DocumentStatus.status_code == code)).first() is None:
                session.add(DocumentStatus(status_name=name, status_code=code))
        session.commit()

        roles = {item.role_name: item for item in session.exec(select(Role)).all()}
        departments = {item.department_name: item for item in session.exec(select(Department)).all()}
        users_seed = [
            ("Айгерім Оқуова", "aigerim@example.invalid", "DeanerySpecialist", "Оқу бөлімі"),
            ("Ерлан Орындаев", "erlan@example.invalid", "Executor", "Тіркеу бөлімі"),
            ("Дана Әкімова", "dana@example.invalid", "Administrator", "Әкімшілік бөлім"),
        ]
        for full_name, email, role_name, department_name in users_seed:
            if session.exec(select(AppUser).where(AppUser.email == email)).first() is None:
                session.add(
                    AppUser(
                        full_name=full_name,
                        email=email,
                        role_id=roles[role_name].role_id,
                        department_id=departments[department_name].department_id,
                    )
                )
        session.commit()

        users = {item.email: item for item in session.exec(select(AppUser)).all()}
        types = {item.code: item for item in session.exec(select(DocumentType)).all()}
        statuses = {item.status_code: item for item in session.exec(select(DocumentStatus)).all()}
        documents_seed = [
            ("DF-DEMO-0001", "Студенттің анықтама беру туралы өтініші", "REQUEST", "IN_PROGRESS", "aigerim@example.invalid"),
            ("DF-DEMO-0002", "Оқу кестесін нақтылау туралы қызметтік хат", "MEMO", "APPROVAL", "dana@example.invalid"),
            ("DF-DEMO-0003", "Академиялық демалыс туралы өтініш", "REQUEST", "REGISTERED", "aigerim@example.invalid"),
            ("DF-DEMO-0004", "Емтихан комиссиясын бекіту жобасы", "ORDER", "COMPLETED", "dana@example.invalid"),
        ]
        for index, (number, title, type_code, status_code, owner_email) in enumerate(documents_seed, start=1):
            if session.exec(select(DeanDocument).where(DeanDocument.registration_number == number)).first() is None:
                session.add(
                    DeanDocument(
                        registration_number=number,
                        title=title,
                        description="Оқу мақсатындағы үлгі дерек.",
                        document_type_id=types[type_code].document_type_id,
                        status_id=statuses[status_code].status_id,
                        department_id=departments["Оқу бөлімі"].department_id,
                        created_by_user_id=users[owner_email].user_id,
                        direction="Ішкі",
                        priority="Жоғары" if index == 1 else "Қалыпты",
                        registered_at=datetime.now(timezone.utc) - timedelta(days=index),
                        due_date=date.today() + timedelta(days=7 - index),
                        completed_at=datetime.now(timezone.utc) if status_code == "COMPLETED" else None,
                    )
                )
        session.commit()

        documents = {item.registration_number: item for item in session.exec(select(DeanDocument)).all()}
        executor = users["erlan@example.invalid"]
        specialist = users["aigerim@example.invalid"]
        for number, title, *_ in documents_seed:
            document = documents[number]
            if session.exec(
                select(ExecutorAssignment).where(
                    ExecutorAssignment.document_id == document.document_id,
                    ExecutorAssignment.user_id == executor.user_id,
                )
            ).first() is None:
                session.add(
                    ExecutorAssignment(
                        document_id=document.document_id,
                        user_id=executor.user_id,
                        assigned_by_user_id=specialist.user_id,
                        task_description=f"{title} құжатын қарау",
                        due_date=document.due_date,
                    )
                )
            if session.exec(
                select(DocumentHistoryEntry).where(
                    DocumentHistoryEntry.document_id == document.document_id,
                    DocumentHistoryEntry.action_type == "Құжат тіркелді",
                )
            ).first() is None:
                session.add(
                    DocumentHistoryEntry(
                        document_id=document.document_id,
                        user_id=specialist.user_id,
                        action_type="Құжат тіркелді",
                        description="Құжат оқу демосына қосылды.",
                    )
                )
            if session.exec(
                select(DocumentComment).where(
                    DocumentComment.document_id == document.document_id,
                    DocumentComment.user_id == specialist.user_id,
                )
            ).first() is None:
                session.add(
                    DocumentComment(
                        document_id=document.document_id,
                        user_id=specialist.user_id,
                        comment_text="Мерзімін және құжат түрін тексеріңіз.",
                    )
                )
            if session.exec(
                select(DocumentAttachment).where(
                    DocumentAttachment.document_id == document.document_id,
                    DocumentAttachment.file_name == f"{number}.pdf",
                )
            ).first() is None:
                session.add(
                    DocumentAttachment(
                        document_id=document.document_id,
                        uploaded_by_user_id=specialist.user_id,
                        file_name=f"{number}.pdf",
                        file_type="application/pdf",
                        file_size=18_000 + index * 1_000,
                    )
                )
        approval_document = documents["DF-DEMO-0002"]
        administrator = users["dana@example.invalid"]
        if session.exec(
            select(DocumentApproval).where(DocumentApproval.document_id == approval_document.document_id)
        ).first() is None:
            session.add(
                DocumentApproval(
                    document_id=approval_document.document_id,
                    approver_user_id=administrator.user_id,
                    approval_order=1,
                    approval_status="Күтуде",
                )
            )
        session.commit()


if __name__ == "__main__":
    seed_database()
    print("Демо деректері дайын.")
