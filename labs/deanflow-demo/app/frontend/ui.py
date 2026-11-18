import os

import pandas as pd
import requests
import streamlit as st


API_URL = os.getenv("BACKEND_URL", "http://127.0.0.1:8000").rstrip("/")
REQUEST_TIMEOUT = 5

st.set_page_config(page_title="DeanFlow құжаттары", page_icon="📄", layout="wide")
st.title("DeanFlow құжат айналымы")
st.caption("Оқу демосы · құжаттарды іздеу, сүзу және тіркеу")


def api_get(path: str, params: dict | None = None):
    response = requests.get(f"{API_URL}{path}", params=params, timeout=REQUEST_TIMEOUT)
    response.raise_for_status()
    return response.json()


def show_api_error(error: Exception) -> None:
    st.error(f"API-ге қосылу мүмкін болмады: {error}")
    st.caption(f"Тексерілетін мекенжай: {API_URL}. Алдымен FastAPI серверін іске қосыңыз.")


try:
    lookups = api_get("/documents/lookups")
except requests.RequestException as error:
    show_api_error(error)
    st.stop()

status_by_name = {"Барлық мәртебе": None}
status_by_name.update({item["name"]: item["code"] for item in lookups["statuses"]})
type_by_name = {item["name"]: item["id"] for item in lookups["types"]}
department_by_name = {"Бөлім таңдалмады": None}
department_by_name.update({item["name"]: item["id"] for item in lookups["departments"]})

with st.sidebar:
    st.subheader("Іздеу және сүзу")
    search = st.text_input("Тіркеу нөмірі немесе тақырып", placeholder="Мысалы: анықтама")
    selected_status = st.selectbox("Мәртебесі", list(status_by_name))
    selected_direction = st.selectbox("Бағыты", ["Барлығы", "Кіріс", "Шығыс", "Ішкі"])
    refresh = st.button("Тізімді жаңарту", use_container_width=True)

params = {"search": search.strip() or None, "status_code": status_by_name[selected_status]}
if selected_direction != "Барлығы":
    params["direction"] = selected_direction
try:
    rows = api_get("/documents", params={key: value for key, value in params.items() if value is not None})
except requests.RequestException as error:
    show_api_error(error)
    rows = []

st.subheader("Құжаттар тізімі")
if rows:
    status_names = {item["id"]: item["name"] for item in lookups["statuses"]}
    type_names = {item["id"]: item["name"] for item in lookups["types"]}
    table = [
        {
            "Тіркеу нөмірі": row["registration_number"],
            "Тақырыбы": row["title"],
            "Түрі": type_names.get(row["document_type_id"], "Белгісіз"),
            "Мәртебесі": status_names.get(row["status_id"], "Белгісіз"),
            "Бағыты": row["direction"],
            "Басымдығы": row["priority"],
            "Тапсыру мерзімі": row["due_date"] or "Көрсетілмеген",
        }
        for row in rows
    ]
    st.dataframe(pd.DataFrame(table), hide_index=True, use_container_width=True)
else:
    st.info("Сүзгіге сәйкес құжат табылмады.")

st.divider()
st.subheader("Жаңа құжат тіркеу")
if not type_by_name:
    st.warning("Белсенді құжат түрлері жоқ. Алдымен анықтамалыққа түр қосыңыз.")
else:
    with st.form("create_document", clear_on_submit=True):
        title = st.text_input("Құжат тақырыбы", max_chars=250)
        description = st.text_area("Қысқаша сипаттамасы", max_chars=3000)
        col1, col2 = st.columns(2)
        with col1:
            type_name = st.selectbox("Құжат түрі", list(type_by_name))
            direction = st.selectbox("Құжат бағыты", ["Кіріс", "Шығыс", "Ішкі"])
        with col2:
            department_name = st.selectbox("Бөлім", list(department_by_name))
            priority = st.selectbox("Басымдығы", ["Төмен", "Қалыпты", "Жоғары", "Шұғыл"])
        submitted = st.form_submit_button("Құжатты тіркеу", type="primary")

    if submitted:
        if len(title.strip()) < 3:
            st.warning("Тақырып кемінде 3 таңбадан тұруы керек.")
        else:
            payload = {
                "title": title.strip(),
                "description": description.strip() or None,
                "document_type_id": type_by_name[type_name],
                "department_id": department_by_name[department_name],
                "direction": direction,
                "priority": priority,
            }
            try:
                response = requests.post(
                    f"{API_URL}/documents", json=payload, timeout=REQUEST_TIMEOUT
                )
                response.raise_for_status()
                created = response.json()
                st.success(f"{created['registration_number']} нөмірлі құжат тіркелді.")
                st.rerun()
            except requests.RequestException as error:
                show_api_error(error)

st.caption("Оқу демосы: мысал деректер SQLite базасында сақталады.")
