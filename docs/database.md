## Tables
- user(**user_id** PK)
- role(**role_id** PK, organization_id FK→organization, store_id FK→store)   -- owner: organization_id OR store_id (or neither, for built-in roles)
- permission(**permission_id** PK)
- user_role(**user_role_id** PK, user_id FK→user, role_id FK→role, organization_id FK→organization, store_id FK→store)   -- the place is organization_id OR store_id
- role_permission(**role_id** FK→role, **permission_id** FK→permission)   -- PK (role_id, permission_id)
- plan(**plan_id** PK)
- feature(**feature_id** PK)
- plan_feature(**plan_id** FK→plan, **feature_id** FK→feature)   -- PK (plan_id, feature_id)
- organization(**organization_id** PK, plan_id FK→plan, owner_user_id FK→user)
- store(**store_id** PK, organization_id FK→organization)
- store_product(**product_id** PK, store_id FK→store)
- store_customer(**customer_id** PK, store_id FK→store)
- store_order(**order_id** PK, store_id FK→store, customer_id FK→store_customer)
- store_order_product(**order_id** FK→store_order, **product_id** FK→store_product)   -- PK (order_id, product_id)
- store_return(**return_id** PK, store_id FK→store, order_id FK→store_order)
- store_return_product(**return_id** FK→store_return, **product_id** FK→store_product)   -- PK (return_id, product_id)


# ER Diagram



# Queries