# DeanFlow зертханалық демосы

Бұл каталог негізгі DeanFlow жүйесіне параллель жұмыс істейтін оқу демосын қамтиды. Ол тапсырмадағы Python құралдарын көрсетеді: FastAPI, SQLModel, Streamlit, SQLite, Docker Compose және pytest. Негізгі ASP.NET Core/SQL Server жүйесімен ортақ өндірістік дерекқор қолданылмайды.

## Жергілікті іске қосу

```powershell
py -3.11 -m venv .venv
.\.venv\Scripts\Activate.ps1
python -m pip install -r requirements.txt
python -m app.database.seed
uvicorn app.main:app --reload
```

Басқа терминалда:

```powershell
.\.venv\Scripts\Activate.ps1
streamlit run app/frontend/ui.py
```

API құжаттамасы `http://127.0.0.1:8000/docs`, UI `http://localhost:8501` мекенжайында ашылады.

## Маршруттар және тесттер

- `GET /documents`: `search`, `status_code`, `direction` query параметрлерімен іздеу.
- `GET /documents/{id}`: бір жазбаны оқу.
- `POST /documents`: валидациядан өткен жаңа құжат тіркеу.
- `PATCH /documents/{id}`: тек берілген өрістерді жаңарту.
- `DELETE /documents/{id}`: құжат пен оған тәуелді демо жазбаларын жою.
- `GET /documents/lookups`: интерфейс формасына қажетті анықтамалық мәндер.

```powershell
pytest -q
```

## Docker Compose

```powershell
docker compose up --build
```

API контейнерінің хосты Compose желісінде `backend` деп аталады; frontend `BACKEND_URL=http://backend:8000` арқылы қосылады. SQLite деректері `deanflow-demo-data` томында сақталады.

Зертханалардың толық сипаттамасы мен қорғауға арналған жауаптарды [негізгі нұсқаулықтан](../../docs/laboratory-guide-kk.md) қараңыз.
