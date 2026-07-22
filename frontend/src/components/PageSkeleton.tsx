import { Card, Col, Row, Skeleton } from 'antd';

/**
 * First-screen skeleton (design spec §7.6): stat cards row + content blocks,
 * avoids layout jumps while the initial payload loads.
 */
export default function PageSkeleton({ cards = 4 }: { cards?: number }) {
  return (
    <div>
      <Row gutter={[24, 24]}>
        {Array.from({ length: cards }).map((_, i) => (
          <Col xs={24} sm={12} lg={24 / cards} key={i}>
            <Card style={{ borderRadius: 18 }}>
              <Skeleton active paragraph={{ rows: 1 }} title={{ width: '40%' }} />
            </Card>
          </Col>
        ))}
      </Row>
      <Row gutter={[24, 24]} style={{ marginTop: 24 }}>
        <Col xs={24} lg={16}>
          <Card style={{ borderRadius: 18 }}>
            <Skeleton active paragraph={{ rows: 6 }} />
          </Card>
        </Col>
        <Col xs={24} lg={8}>
          <Card style={{ borderRadius: 18 }}>
            <Skeleton active paragraph={{ rows: 6 }} />
          </Card>
        </Col>
      </Row>
    </div>
  );
}
