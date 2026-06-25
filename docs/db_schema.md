# Database Schema

## Auth / RBAC
- User(user_id, email, name, phone, password_hash)
- Organization_Membership(org_membership_id, user_id, role_id, organization_id)   -- grants a user a role at the org level
- Store_Membership(store_membership_id, user_id, role_id, store_id)   -- grants a user a role at one store
- Role(role_id, name, organization_id, is_managed, description)   -- is_managed=true: global, we manage it (organization_id null); is_managed=false: custom, owned by organization_id; unique (name, organization_id)
- Permission(permission_id, name, subject, action)   -- subject:action, e.g. store:refund, organization:edit (subject implies the level)
- Role_Permission(role_id, permission_id)

## Plans / Features
- Plan(plan_id, name, description)
- Feature(feature_id, name, description)
- Plan_Feature(plan_id, feature_id)

## Organization
- Organization(organization_id, plan_id, name, owner_user_id, description, country, city, state, zip_code)

## Store
- Store(store_id, organization_id, name, manager, description, country, region, city, state, zip_code)
- Store_Product(store_product_id, store_id, name, description, sku, price, quantity)
- Store_Customer(store_customer_id, store_id, name, email, number)
- Store_Order(store_order_id, store_id, store_customer_id, total, date)
- Store_Order_Product(store_order_id, store_product_id, quantity)
- Store_Return(store_return_id, store_id, store_order_id)
- Store_Return_Product(store_return_id, store_product_id, quantity)
