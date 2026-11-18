from datetime import date


def test_list_documents_returns_a_list(client):
    response = client.get("/documents")

    assert response.status_code == 200
    assert response.json() == []


def test_create_document_and_read_it(client):
    response = client.post(
        "/documents",
        json={
            "title": "Анықтама беру туралы өтініш",
            "document_type_id": 1,
            "status_id": 1,
            "created_by_user_id": 1,
            "direction": "Кіріс",
            "priority": "Қалыпты",
            "due_date": date.today().isoformat(),
        },
    )

    assert response.status_code == 201
    created = response.json()
    assert created["registration_number"].startswith("DF-")
    assert created["title"] == "Анықтама беру туралы өтініш"
    assert client.get(f"/documents/{created['document_id']}").status_code == 200


def test_invalid_document_data_is_rejected(client):
    response = client.post(
        "/documents",
        json={"title": "x", "document_type_id": 1, "status_id": 1, "created_by_user_id": 1},
    )

    assert response.status_code == 422


def test_status_filter_returns_only_matching_documents(client):
    created = client.post(
        "/documents",
        json={
            "title": "Тіркелген құжат",
            "document_type_id": 1,
            "status_id": 1,
            "created_by_user_id": 1,
        },
    )
    assert created.status_code == 201

    matching = client.get("/documents", params={"status_code": "REGISTERED"})
    missing = client.get("/documents", params={"status_code": "COMPLETED"})
    assert len(matching.json()) == 1
    assert missing.json() == []


def test_unknown_document_id_returns_404_for_read_update_and_delete(client):
    assert client.get("/documents/999").status_code == 404
    assert client.patch("/documents/999", json={"title": "Өзгеріс"}).status_code == 404
    assert client.delete("/documents/999").status_code == 404


def test_patch_changes_only_the_sent_field_and_delete_returns_404_afterward(client):
    created = client.post(
        "/documents",
        json={
            "title": "Бастапқы тақырып",
            "document_type_id": 1,
            "status_id": 1,
            "created_by_user_id": 1,
        },
    ).json()
    document_id = created["document_id"]

    patched = client.patch(f"/documents/{document_id}", json={"title": "Жаңартылған тақырып"})
    assert patched.status_code == 200
    assert patched.json()["title"] == "Жаңартылған тақырып"
    assert patched.json()["priority"] == "Қалыпты"

    deleted = client.delete(f"/documents/{document_id}")
    assert deleted.status_code == 204
    assert client.get(f"/documents/{document_id}").status_code == 404


def test_search_treats_input_as_data_not_sql(client):
    response = client.get("/documents", params={"search": "' OR 1=1 --"})

    assert response.status_code == 200
    assert response.json() == []
